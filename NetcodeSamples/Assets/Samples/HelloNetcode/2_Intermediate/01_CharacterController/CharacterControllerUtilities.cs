using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using UnityEngine.Assertions;

namespace Samples.HelloNetcode
{
    // 存储由角色控制器主体施加的脉冲
    public struct DeferredCharacterControllerImpulse
    {
        public Entity Entity;
        public float3 Impulse;
        public float3 Point;
    }

    public static class CharacterControllerUtilities
    {
        const float k_SimplexSolverEpsilon = 0.0001f;
        const float k_SimplexSolverEpsilonSq = k_SimplexSolverEpsilon * k_SimplexSolverEpsilon;

        const int k_DefaultQueryHitsCapacity = 8;
        const int k_DefaultConstraintsCapacity = 2 * k_DefaultQueryHitsCapacity;

        public enum CharacterSupportState : byte
        {
            Unsupported = 0,
            Sliding,
            Supported
        }

        public struct CharacterControllerStepInput
        {
            public PhysicsWorldSingleton PhysicsWorldSingleton;
            public float DeltaTime;
            public float3 Gravity;
            public float3 Up;
            public int MaxIterations;
            public float Tau;
            public float Damping;
            public float SkinWidth;
            public float ContactTolerance;
            public float MaxSlope;
            public int RigidBodyIndex;
            public float3 CurrentVelocity;
            public float MaxMovementSpeed;
        }

        public struct CharacterControllerAllHitsCollector<T> : ICollector<T> where T : unmanaged, IQueryResult
        {
            private int m_selfRBIndex;

            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; }
            public int NumHits => AllHits.Length;

            public float MinHitFraction;
            public NativeList<T> AllHits;
            public NativeList<T> TriggerHits;

            private PhysicsWorld m_world;

            public CharacterControllerAllHitsCollector(int rbIndex, float maxFraction, ref NativeList<T> allHits, PhysicsWorldSingleton physicsWorldSingleton,
                NativeList<T> triggerHits = default)
            {
                MaxFraction = maxFraction;
                AllHits = allHits;
                m_selfRBIndex = rbIndex;
                m_world = physicsWorldSingleton.PhysicsWorld;
                TriggerHits = triggerHits;
                MinHitFraction = float.MaxValue;
            }

            public CharacterControllerAllHitsCollector(int rbIndex, float maxFraction, ref NativeList<T> allHits, PhysicsWorld world,
                NativeList<T> triggerHits = default)
            {
                MaxFraction = maxFraction;
                AllHits = allHits;
                m_selfRBIndex = rbIndex;
                m_world = world;
                TriggerHits = triggerHits;
                MinHitFraction = float.MaxValue;
            }

            #region ICollector

            public bool AddHit(T hit)
            {
                Assert.IsTrue(hit.Fraction <= MaxFraction);

                if (hit.RigidBodyIndex == m_selfRBIndex)
                {
                    return false;
                }

                if (hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents)
                {
                    if (TriggerHits.IsCreated)
                    {
                        TriggerHits.Add(hit);
                    }

                    return false;
                }

                MinHitFraction = math.min(MinHitFraction, hit.Fraction);
                AllHits.Add(hit);
                return true;
            }

            #endregion
        }

        // 一个收集器，仅存储与其自身不同的最接近的命中、触发器以及它命中的预定义值列表。
        public struct CharacterControllerClosestHitCollector<T> : ICollector<T> where T : struct, IQueryResult
        {
            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; private set; }
            public int NumHits { get; private set; }

            private T m_ClosestHit;
            public T ClosestHit => m_ClosestHit;

            private int m_selfRBIndex;
            private PhysicsWorld m_world;

            private NativeList<SurfaceConstraintInfo> m_PredefinedConstraints;

            public CharacterControllerClosestHitCollector(NativeList<SurfaceConstraintInfo> predefinedConstraints, PhysicsWorld world, int rbIndex, float maxFraction)
            {
                MaxFraction = maxFraction;
                m_ClosestHit = default;
                NumHits = 0;
                m_selfRBIndex = rbIndex;
                m_world = world;
                m_PredefinedConstraints = predefinedConstraints;
            }

            public CharacterControllerClosestHitCollector(NativeList<SurfaceConstraintInfo> predefinedConstraints, PhysicsWorldSingleton world, int rbIndex, float maxFraction)
            {
                MaxFraction = maxFraction;
                m_ClosestHit = default;
                NumHits = 0;
                m_selfRBIndex = rbIndex;
                m_world = world.PhysicsWorld;
                m_PredefinedConstraints = predefinedConstraints;
            }

            #region ICollector

            public bool AddHit(T hit)
            {
                Assert.IsTrue(hit.Fraction <= MaxFraction);

                // 检查自我命中和 trigger 命中
                if ((hit.RigidBodyIndex == m_selfRBIndex) || (hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents))
                {
                    return false;
                }

                // 检查预定义的命中
                for (int i = 0; i < m_PredefinedConstraints.Length; i++)
                {
                    SurfaceConstraintInfo constraint = m_PredefinedConstraints[i];
                    if (constraint.RigidBodyIndex == hit.RigidBodyIndex &&
                        constraint.ColliderKey.Equals(hit.ColliderKey))
                    {
                        // 命中已经定义，跳过它
                        return false;
                    }
                }

                // 最后接受打击
                MaxFraction = hit.Fraction;
                m_ClosestHit = hit;
                NumHits = 1;
                return true;
            }

            #endregion
        }

        public static void CheckSupport(
            in PhysicsWorldSingleton physicsWorldSingleton, ref PhysicsCollider collider, CharacterControllerStepInput stepInput, RigidTransform transform,
            out CharacterSupportState characterState, out float3 surfaceNormal, out float3 surfaceVelocity)
        {
            surfaceNormal = float3.zero;
            surfaceVelocity = float3.zero;

            // 向上方向必须标准化
            Assert.IsTrue(Unity.Physics.Math.IsNormalized(stepInput.Up));

            // Query world
            NativeList<ColliderCastHit> castHits = new NativeList<ColliderCastHit>(k_DefaultQueryHitsCapacity, Allocator.Temp);
            CharacterControllerAllHitsCollector<ColliderCastHit> castHitsCollector = new CharacterControllerAllHitsCollector<ColliderCastHit>(
                stepInput.RigidBodyIndex, 1.0f, ref castHits, physicsWorldSingleton);
            var maxDisplacement = -stepInput.ContactTolerance * stepInput.Up;
            {
                ColliderCastInput input = new ColliderCastInput(collider.Value, transform.pos, transform.pos + maxDisplacement, transform.rot);

                physicsWorldSingleton.PhysicsWorld.CastCollider(input, ref castHitsCollector);
            }

            // 如果没有命中，则声明不受支持的状态
            if (castHitsCollector.NumHits == 0)
            {
                characterState = CharacterSupportState.Unsupported;
                return;
            }

            float maxSlopeCos = math.cos(stepInput.MaxSlope);

            // 迭代距离命中并从中创建约束
            NativeList<SurfaceConstraintInfo> constraints = new NativeList<SurfaceConstraintInfo>(k_DefaultConstraintsCapacity, Allocator.Temp);
            float maxDisplacementLength = math.length(maxDisplacement);
            for (int i = 0; i < castHitsCollector.NumHits; i++)
            {
                ColliderCastHit hit = castHitsCollector.AllHits[i];
                CreateConstraint(stepInput.PhysicsWorldSingleton.PhysicsWorld, stepInput.Up,
                    hit.RigidBodyIndex, hit.ColliderKey, hit.Position, hit.SurfaceNormal, hit.Fraction * maxDisplacementLength,
                    stepInput.SkinWidth, maxSlopeCos, ref constraints);
            }

            // 支持检查的速度
            float3 initialVelocity = maxDisplacement / stepInput.DeltaTime;
            Math.ClampToMaxLength(stepInput.MaxMovementSpeed, ref initialVelocity);

            // Solve downwards (don't use min delta time, try to solve full step)
            float3 outVelocity = initialVelocity;
            float3 outPosition = transform.pos;
            SimplexSolver.Solve(stepInput.DeltaTime, stepInput.DeltaTime, stepInput.Up, stepInput.MaxMovementSpeed,
                constraints, ref outPosition, ref outVelocity, out float integratedTime, false);

            // 获取表面信息
            int numSupportingPlanes = 0;
            {
                for (int j = 0; j < constraints.Length; j++)
                {
                    var constraint = constraints[j];
                    if (constraint.Touched && !constraint.IsTooSteep && !constraint.IsMaxSlope)
                    {
                        numSupportingPlanes++;
                        surfaceNormal += constraint.Plane.Normal;
                        surfaceVelocity += constraint.Velocity;
                    }
                }

                if (numSupportingPlanes > 0)
                {
                    float invNumSupportingPlanes = 1.0f / numSupportingPlanes;
                    surfaceNormal *= invNumSupportingPlanes;
                    surfaceVelocity *= invNumSupportingPlanes;

                    surfaceNormal = math.normalize(surfaceNormal);
                }
            }

            // 检查支持状态
            {
                if (math.lengthsq(initialVelocity - outVelocity) < k_SimplexSolverEpsilonSq)
                {
                    // 如果速度没有显着变化，则声明不支持状态
                    characterState = CharacterSupportState.Unsupported;
                }
                else if (math.lengthsq(outVelocity) < k_SimplexSolverEpsilonSq && numSupportingPlanes > 0)
                {
                    // 如果速度很小，则声明支持状态
                    characterState = CharacterSupportState.Supported;
                }
                else
                {
                    // 检查是否滑动
                    outVelocity = math.normalize(outVelocity);
                    float slopeAngleSin = math.max(0.0f, math.dot(outVelocity, -stepInput.Up));
                    float slopeAngleCosSq = 1 - slopeAngleSin * slopeAngleSin;
                    if (slopeAngleCosSq <= maxSlopeCos * maxSlopeCos)
                    {
                        characterState = CharacterSupportState.Sliding;
                    }
                    else if (numSupportingPlanes > 0)
                    {
                        characterState = CharacterSupportState.Supported;
                    }
                    else
                    {
                        // 如果 numSupportingPlanes 为 0，则表面法线无效，因此不支持状态
                        characterState = CharacterSupportState.Unsupported;
                    }
                }
            }
        }

        public static void CollideAndIntegrate(
            CharacterControllerStepInput stepInput, float characterMass, bool affectBodies, ref PhysicsCollider collider,
            ref RigidTransform transform, ref float3 linearVelocity, ref NativeStream.Writer deferredImpulseWriter)
        {
            // 复制参数
            float deltaTime = stepInput.DeltaTime;
            float3 up = stepInput.Up;
            PhysicsWorld world = stepInput.PhysicsWorldSingleton.PhysicsWorld;

            float remainingTime = deltaTime;

            float3 newPosition = transform.pos;
            quaternion orientation = transform.rot;
            float3 newVelocity = linearVelocity;

            float maxSlopeCos = math.cos(stepInput.MaxSlope);

            const float timeEpsilon = 0.000001f;
            for (int i = 0; i < stepInput.MaxIterations && remainingTime > timeEpsilon; i++)
            {
                NativeList<SurfaceConstraintInfo> constraints = new NativeList<SurfaceConstraintInfo>(k_DefaultConstraintsCapacity, Allocator.Temp);

                // 进行 collider 演员表
                {
                    float3 displacement = newVelocity * remainingTime;
                    NativeList<ColliderCastHit> triggerHits = default;
                    NativeList<ColliderCastHit> castHits = new NativeList<ColliderCastHit>(k_DefaultQueryHitsCapacity, Allocator.Temp);
                    CharacterControllerAllHitsCollector<ColliderCastHit> collector = new CharacterControllerAllHitsCollector<ColliderCastHit>(
                        stepInput.RigidBodyIndex, 1.0f, ref castHits, stepInput.PhysicsWorldSingleton, triggerHits);
                    ColliderCastInput input = new ColliderCastInput(collider.Value, newPosition, newPosition + displacement, orientation);
                    stepInput.PhysicsWorldSingleton.PhysicsWorld.CastCollider(input, ref collector);

                    // 迭代命中并从中创建约束
                    for (int hitIndex = 0; hitIndex < collector.NumHits; hitIndex++)
                    {
                        ColliderCastHit hit = collector.AllHits[hitIndex];
                        CreateConstraint(stepInput.PhysicsWorldSingleton.PhysicsWorld, stepInput.Up,
                            hit.RigidBodyIndex, hit.ColliderKey, hit.Position, hit.SurfaceNormal, math.dot(-hit.SurfaceNormal, hit.Fraction * displacement),
                            stepInput.SkinWidth, maxSlopeCos, ref constraints);
                    }
                }

                // 然后做一个 collider 距离进行穿透恢复，
                // 但只修复穿透性命中
                {
                    // Collider 距离 query
                    NativeList<DistanceHit> distanceHits = new NativeList<DistanceHit>(k_DefaultQueryHitsCapacity, Allocator.Temp);
                    CharacterControllerAllHitsCollector<DistanceHit> distanceHitsCollector = new CharacterControllerAllHitsCollector<DistanceHit>(
                        stepInput.RigidBodyIndex, stepInput.ContactTolerance, ref distanceHits, stepInput.PhysicsWorldSingleton);
                    {
                        ColliderDistanceInput input = new ColliderDistanceInput(collider.Value, stepInput.ContactTolerance, transform);
                        stepInput.PhysicsWorldSingleton.PhysicsWorld.CalculateDistance(input, ref distanceHitsCollector);
                    }

                    // 迭代穿透命中并修复距离和正常
                    int numConstraints = constraints.Length;
                    for (int hitIndex = 0; hitIndex < distanceHitsCollector.NumHits; hitIndex++)
                    {
                        DistanceHit hit = distanceHitsCollector.AllHits[hitIndex];
                        if (hit.Distance < stepInput.SkinWidth)
                        {
                            bool found = false;

                            // 向后迭代以在最大斜率 constraint 之前找到原始 constraint
                            for (int constraintIndex = numConstraints - 1; constraintIndex >= 0; constraintIndex--)
                            {
                                SurfaceConstraintInfo constraint = constraints[constraintIndex];
                                if (constraint.RigidBodyIndex == hit.RigidBodyIndex &&
                                    constraint.ColliderKey.Equals(hit.ColliderKey))
                                {
                                    // 修复 constraint（正常，距离）
                                    {
                                        // 创建新的 constraint
                                        CreateConstraintFromHit(stepInput.PhysicsWorldSingleton.PhysicsWorld, hit.RigidBodyIndex, hit.ColliderKey,
                                            hit.Position, hit.SurfaceNormal, hit.Distance,
                                            stepInput.SkinWidth, out SurfaceConstraintInfo newConstraint);

                                        // 解决其渗透
                                        ResolveConstraintPenetration(ref newConstraint);

                                        // 回信
                                        constraints[constraintIndex] = newConstraint;
                                    }

                                    found = true;
                                    break;
                                }
                            }

                            // 添加 collider 施法未捕获的穿透击中
                            if (!found)
                            {
                                CreateConstraint(stepInput.PhysicsWorldSingleton.PhysicsWorld, stepInput.Up,
                                    hit.RigidBodyIndex, hit.ColliderKey, hit.Position, hit.SurfaceNormal, hit.Distance,
                                    stepInput.SkinWidth, maxSlopeCos, ref constraints);
                            }
                        }
                    }
                }

                // 求解器中断的最小增量时间
                float minDeltaTime = 0.0f;
                if (math.lengthsq(newVelocity) > k_SimplexSolverEpsilonSq)
                {
                    // 移动至少 1 厘米的最小增量时间
                    minDeltaTime = 0.01f / math.length(newVelocity);
                }

                // 解决
                float3 prevVelocity = newVelocity;
                float3 prevPosition = newPosition;
                SimplexSolver.Solve(remainingTime, minDeltaTime, up, stepInput.MaxMovementSpeed, constraints, ref newPosition, ref newVelocity, out float integratedTime);

                // 应用脉冲来撞击物体并存储碰撞事件
                if (affectBodies)
                {
                    CalculateAndStoreDeferredImpulsesAndCollisionEvents(stepInput, affectBodies, characterMass,
                        prevVelocity, constraints, ref deferredImpulseWriter);
                }

                // 计算新的位移
                float3 newDisplacement = newPosition - prevPosition;

                // 如果单纯形解算器移动了角色，我们需要重新投射以确保它可以移动到新位置
                if (math.lengthsq(newDisplacement) > k_SimplexSolverEpsilon)
                {
                    // 检查我们是否可以走到单纯形求解器建议的位置
                    var newCollector = new CharacterControllerClosestHitCollector<ColliderCastHit>(constraints, stepInput.PhysicsWorldSingleton, stepInput.RigidBodyIndex, 1.0f);

                    ColliderCastInput input = new ColliderCastInput(collider.Value, prevPosition, prevPosition + newDisplacement, orientation);

                    stepInput.PhysicsWorldSingleton.PhysicsWorld.CastCollider(input, ref newCollector);

                    if (newCollector.NumHits > 0)
                    {
                        ColliderCastHit hit = newCollector.ClosestHit;

                        // 沿 newDisplacement 方向移动角色，直到到达此新联系人
                        {
                            Assert.IsTrue(hit.Fraction >= 0.0f && hit.Fraction <= 1.0f);

                            integratedTime *= hit.Fraction;
                            newPosition = prevPosition + newDisplacement * hit.Fraction;
                        }
                    }
                }

                // 减少剩余时间
                remainingTime -= integratedTime;

                // 写回位置，以便距离 query 将更新结果
                transform.pos = newPosition;
            }

            // 写回最终速度
            linearVelocity = newVelocity;
        }

        private static void CreateConstraintFromHit(PhysicsWorld world, int rigidBodyIndex, ColliderKey colliderKey,
            float3 hitPosition, float3 normal, float distance, float skinWidth, out SurfaceConstraintInfo constraint)
        {
            bool bodyIsDynamic = 0 <= rigidBodyIndex && rigidBodyIndex < world.NumDynamicBodies;
            constraint = new SurfaceConstraintInfo()
            {
                Plane = new Unity.Physics.Plane
                {
                    Normal = normal,
                    Distance = distance - skinWidth,
                },
                RigidBodyIndex = rigidBodyIndex,
                ColliderKey = colliderKey,
                HitPosition = hitPosition,
                Velocity = bodyIsDynamic ? world.GetLinearVelocity(rigidBodyIndex, hitPosition) : float3.zero,
                Priority = bodyIsDynamic ? 1 : 0
            };
        }

        private static void CreateMaxSlopeConstraint(float3 up, ref SurfaceConstraintInfo constraint, out SurfaceConstraintInfo maxSlopeConstraint)
        {
            float verticalComponent = math.dot(constraint.Plane.Normal, up);

            SurfaceConstraintInfo newConstraint = constraint;
            newConstraint.Plane.Normal = math.normalize(newConstraint.Plane.Normal - verticalComponent * up);
            newConstraint.IsMaxSlope = true;

            float distance = newConstraint.Plane.Distance;

            // 计算沿新法线到原始平面的距离。
            // 将新距离限制为旧距离的 2 倍，以避免穿透恢复爆炸。
            newConstraint.Plane.Distance = distance / math.max(math.dot(newConstraint.Plane.Normal, constraint.Plane.Normal), 0.5f);

            if (newConstraint.Plane.Distance < 0.0f)
            {
                // 禁用原始平面的穿透恢复
                constraint.Plane.Distance = 0.0f;

                // 准备速度以解决渗透问题
                ResolveConstraintPenetration(ref newConstraint);
            }

            // 输出最大斜率 constraint
            maxSlopeConstraint = newConstraint;
        }

        private static void ResolveConstraintPenetration(ref SurfaceConstraintInfo constraint)
        {
            // 修复速度以实现穿透恢复
            if (constraint.Plane.Distance < 0.0f)
            {
                float3 newVel = constraint.Velocity - constraint.Plane.Normal * constraint.Plane.Distance;
                constraint.Velocity = newVel;
                constraint.Plane.Distance = 0.0f;
            }
        }

        private static void CreateConstraint(PhysicsWorld world, float3 up,
            int hitRigidBodyIndex, ColliderKey hitColliderKey, float3 hitPosition, float3 hitSurfaceNormal, float hitDistance,
            float skinWidth, float maxSlopeCos, ref NativeList<SurfaceConstraintInfo> constraints)
        {
            CreateConstraintFromHit(world, hitRigidBodyIndex, hitColliderKey, hitPosition,
                hitSurfaceNormal, hitDistance, skinWidth, out SurfaceConstraintInfo constraint);

            // 检查是否需要最大坡度平面
            float verticalComponent = math.dot(constraint.Plane.Normal, up);
            bool shouldAddPlane = verticalComponent > k_SimplexSolverEpsilon && verticalComponent < maxSlopeCos;
            if (shouldAddPlane)
            {
                constraint.IsTooSteep = true;
                CreateMaxSlopeConstraint(up, ref constraint, out SurfaceConstraintInfo maxSlopeConstraint);
                constraints.Add(maxSlopeConstraint);
            }

            // 准备速度以解决渗透问题
            ResolveConstraintPenetration(ref constraint);

            // 将原始 constraint 添加到列表中
            constraints.Add(constraint);
        }

        private static void CalculateAndStoreDeferredImpulsesAndCollisionEvents(
            CharacterControllerStepInput stepInput, bool affectBodies, float characterMass,
            float3 linearVelocity, NativeList<SurfaceConstraintInfo> constraints, ref NativeStream.Writer deferredImpulseWriter)
        {
            PhysicsWorld world = stepInput.PhysicsWorldSingleton.PhysicsWorld;
            for (int i = 0; i < constraints.Length; i++)
            {
                SurfaceConstraintInfo constraint = constraints[i];
                int rigidBodyIndex = constraint.RigidBodyIndex;

                float3 impulse = float3.zero;

                if (rigidBodyIndex < 0)
                {
                    continue;
                }

                // 如果需要计算冲量，请跳过静态物体
                if (affectBodies && (rigidBodyIndex < world.NumDynamicBodies))
                {
                    RigidBody body = world.Bodies[rigidBodyIndex];

                    float3 pointRelVel = world.GetLinearVelocity(rigidBodyIndex, constraint.HitPosition);
                    pointRelVel -= linearVelocity;

                    float projectedVelocity = math.dot(pointRelVel, constraint.Plane.Normal);

                    // 所需的速度变化
                    float deltaVelocity = -projectedVelocity * stepInput.Damping;

                    float distance = constraint.Plane.Distance;
                    if (distance < 0.0f)
                    {
                        deltaVelocity += (distance / stepInput.DeltaTime) * stepInput.Tau;
                    }

                    // 计算脉冲
                    MotionVelocity mv = world.MotionVelocities[rigidBodyIndex];
                    if (deltaVelocity < 0.0f)
                    {
                        // 脉冲幅度
                        float impulseMagnitude = 0.0f;
                        {
                            float objectMassInv = GetInvMassAtPoint(constraint.HitPosition, constraint.Plane.Normal, body, mv);
                            impulseMagnitude = deltaVelocity / objectMassInv;
                        }

                        impulse = impulseMagnitude * constraint.Plane.Normal;
                    }

                    // 添加重力
                    {
                        // 重力对法线方向上角色速度的影响
                        float3 charVelDown = stepInput.Gravity * stepInput.DeltaTime;
                        float relVelN = math.dot(charVelDown, constraint.Plane.Normal);

                        // 如果分离接触则减去分离速度
                        {
                            bool isSeparatingContact = projectedVelocity < 0.0f;
                            float newRelVelN = relVelN - projectedVelocity;
                            relVelN = math.select(relVelN, newRelVelN, isSeparatingContact);
                        }

                        // 如果生成的速度为负，则会施加脉冲来停止角色
                        // 以免落入体内
                        {
                            float3 newImpulse = impulse;
                            newImpulse += relVelN * characterMass * constraint.Plane.Normal;
                            impulse = math.select(impulse, newImpulse, relVelN < 0.0f);
                        }
                    }

                    // 储存冲动
                    deferredImpulseWriter.Write(
                        new DeferredCharacterControllerImpulse()
                        {
                            Entity = body.Entity,
                            Impulse = impulse,
                            Point = constraint.HitPosition
                        });
                }
            }
        }

        static float GetInvMassAtPoint(float3 point, float3 normal, RigidBody body, MotionVelocity mv)
        {
            var massCenter =
                math.transform(body.WorldFromBody, body.Collider.Value.MassProperties.MassDistribution.Transform.pos);
            float3 arm = point - massCenter;
            float3 jacAng = math.cross(arm, normal);
            float3 armC = jacAng * mv.InverseInertia;

            float objectMassInv = math.dot(armC, jacAng);
            objectMassInv += mv.InverseMass;

            return objectMassInv;
        }
    }
}
