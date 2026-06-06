#if !UNITY_DISABLE_MANAGED_COMPONENTS
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// 描述动画 system 要扮演的角色的当前状态。
    /// 状态，例如运动方向、目标方向等。
    /// system <see cref="UpdateAnimationStateSystem"/> 将调用 <see cref="CharacterAnimation.UpdateAnimationState"/>
    /// 从玩家 entity 收集的信息。
    /// </summary>
    public struct CharacterAnimationData
    {
        public bool OnGround;
        public bool IsShooting;
        public float Pitch;
        public float Yaw;
        public float2 Movement;
    }

    /// <summary>
    /// 根据从 <see cref="UpdateAnimationState"/> system 发送的 <see cref="CharacterAnimationData"/> 更新动画器。
    /// 预计具有相关控制器的 Animator 会附加到同一个游戏对象。
    /// </summary>
    public class CharacterAnimation : MonoBehaviour
    {
        public bool IgnoreEvents;

        const float k_TurnAngle = 90.0f;

        Animator m_Animator;
        float m_RemainingTurnAngle;
        float m_AirPhase;

        enum CharacterAnimationState
        {
            Stand,
            Run,
            Jump,
        }

        static readonly int VelX = Animator.StringToHash("VelX");
        static readonly int VelY = Animator.StringToHash("VelY");
        static readonly int Moving = Animator.StringToHash("Moving");
        static readonly int TurnDirection = Animator.StringToHash("TurnDirection");
        static readonly int TurnTime = Animator.StringToHash("TurnTime");
        static readonly int AimPitch = Animator.StringToHash("AimPitch");
        static readonly int AimYaw = Animator.StringToHash("AimYaw");
        static readonly int Jumping = Animator.StringToHash("Jumping");
        static readonly int Shooting = Animator.StringToHash("Shooting");

        /// <summary>
        /// 用于根据动画剪辑的长度控制角色的旋转
        /// </summary>
        public AnimationClip TurnAnimationClip;
        public Transform RightOffhandIk;
        public Vector3 Offset = new Vector3(-90,0,-90);

        void Start()
        {
            m_Animator = GetComponent<Animator>();
            m_Animator.fireEvents = !IgnoreEvents;
        }

        public LocalTransform UpdateAnimationState(CharacterAnimationData data, LocalTransform localTransform)
        {
            if (m_Animator == null)
            {
                return localTransform;
            }

            UpdateAim(data.Pitch);

            var state = ComputeAnimationState(data.OnGround, data.IsShooting, data.Movement);
            switch (state)
            {
                case CharacterAnimationState.Stand:
                    return StandingAnimation(data.Yaw, localTransform);
                case CharacterAnimationState.Run:
                    RunAnimation(data.Movement.x, data.Movement.y);
                    return UpdateRotation(data.Yaw, localTransform);
                case CharacterAnimationState.Jump:
                    return UpdateRotation(data.Yaw, localTransform);
            }

            return localTransform;
        }

        /// <summary>
        /// 返回 <see cref="float.Epsilon"/> 内两个 <see cref="float2"/>s 是否相等
        /// </summary>
        static bool NearlyEqual(float2 a, float2 b)
        {
            return math.abs(a.x - b.x) <= float.Epsilon && math.abs(a.y - b.y) <= float.Epsilon;
        }

        /// <summary>
        /// 返回 <see cref="CharacterAnimationState"/> 并相应地设置动画器状态。
        /// E.g。如果输入 system 表示角色不在地面上，
        /// 动画状态应该是跳跃。
        /// </summary>
        CharacterAnimationState ComputeAnimationState(bool onGround, bool isShooting, float2 movement)
        {
            m_Animator.SetBool(Jumping, !onGround);
            if (isShooting) { m_Animator.SetTrigger(Shooting); }
            if (!onGround)
            {
                return CharacterAnimationState.Jump;
            }

            if (NearlyEqual(movement, float2.zero))
            {
                m_Animator.SetBool(Moving, false);
                return CharacterAnimationState.Stand;
            }

            m_Animator.SetBool(Moving, true);
            return CharacterAnimationState.Run;

        }

        /// <summary>
        /// 更新角色的音调。
        /// 总共限制为 180 度。向下和向上 90 度。
        /// </summary>
        void UpdateAim(float pitch)
        {
            var aimPitch = 90 + pitch * 180.0f / 3.1415f;
            var aimPitchFraction = aimPitch / 180.0f;
            m_Animator.SetFloat(AimPitch, aimPitchFraction);
        }

        /// <summary>
        /// 返回 <paramref name="transform"/>，旋转设置为 <paramref name="yawRadians"/>。
        /// </summary>
        static LocalTransform UpdateRotation(float yawRadians, LocalTransform transform)
        {
            var rot = quaternion.RotateY(yawRadians);
            return LocalTransform.FromPositionRotation(transform.Position, rot);
        }

        /// <summary>
        /// 更新混合树使用的两个动画浮动
        /// 在动画器状态机中确定 run 方向。
        ///
        /// 这些值将在 0 和 1 之间标准化
        /// </summary>
        void RunAnimation(float horizontal, float vertical)
        {
            var moveInput = new Vector3(horizontal, 0, vertical);
            var normalized = moveInput.normalized;
            var normalInput2D = new Vector2(normalized.x, normalized.z);

            m_Animator.SetFloat(VelX, normalInput2D.x);
            m_Animator.SetFloat(VelY, normalInput2D.y);
        }

        /// <summary>
        /// 当站立不动时，角色将转动一次 <paramref name="yawRadians"/> 转换为度数
        /// 超过 <see cref="k_TurnAngle"/> 常数。
        /// 该回合将使用 <see cref="Time.deltaTime"/> 每帧更新。
        /// </summary>
        LocalTransform StandingAnimation(float yawRadians, LocalTransform localTransform)
        {
            var yaw = math.degrees(yawRadians);
            var eulerAnglesY = ((Quaternion)localTransform.Rotation).eulerAngles.y;
            var aimDelta = Mathf.DeltaAngle(eulerAnglesY, yaw);

            if (m_RemainingTurnAngle == 0 && math.abs(aimDelta) > k_TurnAngle)
            {
                m_RemainingTurnAngle = k_TurnAngle * math.sign(aimDelta);
            }

            var aimYawFraction = aimDelta / k_TurnAngle;
            m_Animator.SetFloat(AimYaw, aimYawFraction);

            return UpdateTurn(localTransform);
        }

        LocalTransform UpdateTurn(LocalTransform localTransform)
        {
            var sign = math.sign(m_RemainingTurnAngle);
            return UpdateTurnAnimation(sign)
                ? localTransform
                : UpdateTurnTransform(sign, localTransform);
        }

        LocalTransform UpdateTurnTransform(float sign, LocalTransform localTransform)
        {
            var absRotationThisFrame = Time.deltaTime * k_TurnAngle / TurnAnimationClip.length;
            if (absRotationThisFrame >= math.abs(m_RemainingTurnAngle))
            {
                absRotationThisFrame = math.abs(m_RemainingTurnAngle);
                m_RemainingTurnAngle = 0;
            }
            else
            {
                m_RemainingTurnAngle -= absRotationThisFrame * sign;
            }

            var x = math.mul(quaternion.RotateY(math.radians(absRotationThisFrame * sign)), localTransform.Rotation);
            return localTransform.WithRotation(x);
        }

        bool UpdateTurnAnimation(float sign)
        {
            if (m_RemainingTurnAngle == 0)
            {
                m_Animator.SetFloat(TurnDirection, 0);
                return true;
            }

            var fraction = 1f - math.abs(m_RemainingTurnAngle / (k_TurnAngle * sign));
            m_Animator.SetFloat(TurnTime, fraction);
            m_Animator.SetFloat(TurnDirection, sign);
            return false;
        }

        /// <summary>
        /// 这将使用 <see cref="RightOffhandIk"/> 点将左手连接到枪上。
        /// </summary>
        void OnAnimatorIK(int layerIndex)
        {
            if (m_Animator == null) { return; }
            // 头像指向左手 IK 左
            m_Animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 100);
            m_Animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 100);
            m_Animator.SetIKPosition(AvatarIKGoal.LeftHand, RightOffhandIk.position);
            m_Animator.SetIKRotation(AvatarIKGoal.LeftHand, RightOffhandIk.rotation * Quaternion.Euler(Offset));
        }
    }
}
#endif
