using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using static Unity.Physics.Math;

public class SoftJointDemoScene : SceneCreationSettings {}

public class SoftJointDemo : SceneCreationAuthoring<SoftJointDemoScene>
{
    class SoftJointDemoBaker : Baker<SoftJointDemo>
    {
        public override void Bake(SoftJointDemo authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new SoftJointDemoScene
            {
                DynamicMaterial = authoring.DynamicMaterial,
                StaticMaterial = authoring.StaticMaterial
            });
        }
    }
}

public partial class SoftJointDemoCreationSystem : SceneCreationSystem<SoftJointDemoScene>
{
    public override void CreateScene(SoftJointDemoScene sceneSettings)
    {
        // 制作软球和插座
        {
            BlobAssetReference<Unity.Physics.Collider> collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = new float3(0.2f, 0.2f, 0.2f),
                BevelRadius = 0.0f
            });
            CreatedColliders.Add(collider);

            // 用不同的弹簧频率制作接头。最左边的 joint 应以 0.5hz 振荡，下一个以 1hz 振荡，下一个以 1.5hz 振荡，依此类推。
            for (int i = 0; i < 10; i++)
            {
                // 创建一个身体
                float3 position = new float3((i - 4.5f) * 1.0f, 0, 0);
                float3 velocity = new float3(0, -10.0f, 0);
                Entity body = CreateDynamicBody(
                    position, quaternion.identity, collider, velocity, float3.zero, 1.0f);

                // 创建球窝 joint
                float3 pivotLocal = float3.zero;
                float3 pivotInWorld = math.transform(GetBodyTransform(body), pivotLocal);

                var jointData = PhysicsJoint.CreateBallAndSocket(pivotLocal, pivotInWorld);
                var constraints = jointData.GetConstraints();
                var constraint = constraints[0];
                // 选择较小的阻尼值而不是 0，以提高关节的稳定性
                constraint.DampingRatio = 0.05f;
                constraint.SpringFrequency = 0.5f * (float)(i + 1);
                constraints[0] = constraint;
                jointData.SetConstraints(constraints);

                CreateJoint(jointData, body, Entity.Null);
            }
        }

        //制作软限位铰链
        {
            BlobAssetReference<Unity.Physics.Collider> collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = new float3(0.4f, 0.1f, 0.6f),
                BevelRadius = 0.0f
            });
            CreatedColliders.Add(collider);

            // 第一排有软限制，带有硬铰链+枢轴，第二排有所有软限制
            for (int j = 0; j < 2; j++)
            {
                for (int i = 0; i < 10; i++)
                {
                    // 创建一个身体
                    float3 position = new float3((i - 4.5f) * 1.0f, 0, (j + 1) * 3.0f);
                    float3 velocity = new float3(0, -10.0f, 0);
                    float3 angularVelocity = new float3(0, 0, -10.0f);
                    Entity body = CreateDynamicBody(
                        position, quaternion.identity, collider, velocity, angularVelocity, 1.0f);

                    // 创建有限铰链 joint
                    float3 pivotLocal = new float3(0, 0, 0);
                    float3 pivotInWorld = math.transform(GetBodyTransform(body), pivotLocal);
                    float3 axisLocal = new float3(0, 0, 1);
                    float3 axisInWorld = axisLocal;
                    float3 perpendicularLocal = new float3(0, 1, 0);
                    float3 perpendicularInWorld = perpendicularLocal;

                    var frameLocal = new BodyFrame { Axis = axisLocal, PerpendicularAxis = perpendicularLocal, Position = pivotLocal };
                    var frameWorld = new BodyFrame { Axis = axisInWorld, PerpendicularAxis = perpendicularInWorld, Position = pivotInWorld };
                    var jointData = PhysicsJoint.CreateLimitedHinge(frameLocal, frameWorld, default);

                    // 第一个 constraint 是极限，接下来两个是铰链和枢轴
                    var constraints = jointData.GetConstraints();
                    for (int k = 0; k < 1 + 2 * j; k++)
                    {
                        var constraint = constraints[k];
                        // 选择较小的阻尼值而不是 0，以提高关节的稳定性
                        constraint.DampingRatio = 0.05f;
                        constraint.SpringFrequency = 0.5f * (i + 1);
                        constraints[k] = constraint;
                    }
                    jointData.SetConstraints(constraints);

                    CreateJoint(jointData, body, Entity.Null);
                }
            }
        }

        // 制作一个软棱柱体
        {
            BlobAssetReference<Unity.Physics.Collider> collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = new float3(0.2f, 0.2f, 0.2f),
                BevelRadius = 0.0f
            });
            CreatedColliders.Add(collider);

            // 创建一个身体
            float3 position = new float3(0, 0, 9.0f);
            float3 velocity = new float3(50.0f, 0, 0);
            Entity body = CreateDynamicBody(
                position, quaternion.identity, collider, velocity, float3.zero, 1.0f);

            // 创建棱柱 joint
            float3 pivotLocal = float3.zero;
            float3 pivotInWorld = math.transform(GetBodyTransform(body), pivotLocal);
            float3 axisLocal = new float3(1, 0, 0);
            float3 axisInWorld = axisLocal;
            float3 perpendicularLocal = new float3(0, 1, 0);
            float3 perpendicularInWorld = perpendicularLocal;

            var localFrame = new BodyFrame { Axis = axisLocal, PerpendicularAxis = perpendicularLocal, Position = pivotLocal };
            var worldFrame = new BodyFrame { Axis = axisInWorld, PerpendicularAxis = perpendicularInWorld, Position = pivotInWorld };
            var jointData = PhysicsJoint.CreatePrismatic(localFrame, worldFrame, new FloatRange(-2f, 2f));
            var constraints = jointData.GetConstraints();
            var constraint = constraints[0];
            // 选择较小的阻尼值而不是 0，以提高关节的稳定性
            constraint.DampingRatio = 0.05f;
            constraint.SpringFrequency = 5.0f;
            constraints[0] = constraint;
            jointData.SetConstraints(constraints);
            CreateJoint(jointData, body, Entity.Null);
        }
    }
}
