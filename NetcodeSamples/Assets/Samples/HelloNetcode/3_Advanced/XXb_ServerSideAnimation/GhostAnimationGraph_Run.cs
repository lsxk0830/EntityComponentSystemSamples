using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Unity.Mathematics;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
#if !UNITY_DISABLE_MANAGED_COMPONENTS
using Unity.NetCode.Hybrid;
#endif

namespace Samples.HelloNetcode.Hybrid
{

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    public struct LocomotionAnimationData : IComponentData
    {
        [GhostField(Quantization=1000)] public float2 Direction;
        [GhostField(Quantization=1000)] public float Phase;
        [GhostField(Quantization=1000)] public float aimPitch;
    }
    public class RunGhostPlayableBehaviour : GhostPlayableBehaviour
    {
        GhostAnimationController m_controller;
        AnimationMixerPlayable m_mixer;
        private AnimationClipPlayable[] m_clips;
        private float2[] m_positions;

        private float[] m_clipLengths;
        private float[] m_weights;

        AnimationClipPlayable m_clipAim;

        // OnPlayableCreate
        // OnPlayableDestroy

        public override void PreparePredictedData(NetworkTick serverTick, float deltaTime, bool isRollback)
        {
            ref var locoData = ref m_controller.GetPlayableDataRef<LocomotionAnimationData>();

            var input = m_controller.GetEntityComponentData<CharacterControllerPlayerInput>();
            var character = m_controller.GetEntityComponentData<Character>();


            var trans = m_controller.GetEntityComponentData<LocalTransform>();

            locoData.aimPitch = 90 + input.Pitch * 180.0f / 3.1415f;

            if ((input.Movement.x != 0 || input.Movement.y != 0) && character.OnGround == 1)
            {
                trans.Rotation = quaternion.RotateY(input.Yaw);
                m_controller.SetEntityComponentData(trans);
            }



            locoData.Direction = math.normalizesafe(input.Movement);

            var blendedClipLength = CalculateWeights(m_positions, m_clipLengths, m_weights, locoData.Direction);
            locoData.Phase += deltaTime / blendedClipLength;
        }

        public override void PrepareFrame(Playable playable, FrameData info)
        {
            // FIXME: 确保 locomotionData 是只读的
            var locoData = m_controller.GetPlayableData<LocomotionAnimationData>();
            var blendedClipLength = CalculateWeights(m_positions, m_clipLengths, m_weights, locoData.Direction);
            for (var i = 0; i < m_clips.Length; i++)
            {
                m_mixer.SetInputWeight(i, m_weights[i]);

                m_clips[i].SetSpeed(m_clipLengths[i] / blendedClipLength);
                m_clips[i].SetTime(locoData.Phase * m_clipLengths[i]);
            }
            // 更新目标
            float aimPitchFraction = locoData.aimPitch / 180.0f;
            m_clipAim.SetTime(aimPitchFraction * m_clipAim.GetDuration());

            base.PrepareFrame(playable, info);
        }
        public void Initialize(GhostAnimationController controller, PlayableGraph graph, Playable owner, List<GhostAnimationGraph_Run.BlendSpaceNode> blendSpaceNodes, AnimationClip aimClip)
        {
            m_controller = controller;
            m_mixer = AnimationMixerPlayable.Create(graph, blendSpaceNodes.Count);

            m_clips = new AnimationClipPlayable[blendSpaceNodes.Count];
            m_positions = new float2[blendSpaceNodes.Count];
            m_clipLengths = new float[blendSpaceNodes.Count];
            m_weights = new float[blendSpaceNodes.Count];

            for (var i = 0; i < blendSpaceNodes.Count; i++)
            {
                var node = blendSpaceNodes[i];
                var clip = AnimationClipPlayable.Create(graph, node.clip);
                m_positions[i] = math.normalizesafe(node.position);
                m_clipLengths[i] = node.clip.length;
                m_mixer.ConnectInput(i, clip, 0);
                m_clips[i] = clip;
                m_mixer.SetInputWeight(i, 0);
            }

            m_clipAim = AnimationClipPlayable.Create(graph, aimClip);
            //m_clipAim.SetApplyFootIK(false);
            //m_clipAim.Pause();
            m_clipAim.SetDuration(aimClip.length);

            // 设置其他添加剂混合器
            var additiveMixer = AnimationLayerMixerPlayable.Create(graph);
            var locoMixerPort = additiveMixer.AddInput(m_mixer, 0);
            additiveMixer.SetInputWeight(locoMixerPort, 1);

            var aimMixerPort = additiveMixer.AddInput(m_clipAim, 0);
            additiveMixer.SetInputWeight(aimMixerPort, 1);
            additiveMixer.SetLayerAdditive((uint)aimMixerPort, true);

            owner.SetInputCount(1);
            graph.Connect(additiveMixer, 0, owner, 0);
            owner.SetInputWeight(0, 1);
        }
        static float CalculateWeights(float2[] positionArray, float[] clipLengthArray, float[] weightArray, float2 blendPosition)
        {
            var count = positionArray.Length;

            // 将所有权重初始化为 0
            for (var i = 0; i < weightArray.Length; i++)
            {
                weightArray[i] = 0f;
            }

            // 处理后备
            if (count < 2)
            {
                if (count == 1)
                {
                    weightArray[0] = 1;
                    return clipLengthArray[0];
                }
                return 1;
            }

            // 处理中间精确采样时的特殊情况
            if (math.all(blendPosition == float2.zero))
            {
                // 如果我们有一个中心运动，则赋予它所有的权重
                for (var i = 0; i < count; i++)
                {
                    if (math.all(positionArray[i] == float2.zero))
                    {
                        weightArray[i] = 1;
                        return clipLengthArray[i];
                    }
                }

                // 否则平均分配重量
                float sharedWeight = 1.0f / count;
                float avgCenterLen = 0;
                for (var i = 0; i < count; i++)
                {
                    weightArray[i] = sharedWeight;
                    avgCenterLen += sharedWeight * clipLengthArray[i];
                }

                return avgCenterLen;
            }

            int indexA = -1;
            int indexB = -1;
            int indexCenter = -1;
            float maxDotForNegCross = -100000.0f;
            float maxDotForPosCross = -100000.0f;
            for (var i = 0; i < count; i++)
            {
                if (math.all(positionArray[i] == float2.zero))
                {
                    if (indexCenter >= 0)
                        return 1;
                    indexCenter = i;
                    continue;
                }

                var posNormalized = positionArray[i];
                var dot = math.dot(posNormalized, blendPosition);
                var cross = posNormalized.x * blendPosition.y - posNormalized.y * blendPosition.x;
                if (cross > 0f)
                {
                    if (dot > maxDotForPosCross)
                    {
                        maxDotForPosCross = dot;
                        indexA = i;
                    }
                }
                else
                {
                    if (dot > maxDotForNegCross)
                    {
                        maxDotForNegCross = dot;
                        indexB = i;
                    }
                }
            }

            float centerWeight = 0;
            float avgLen = 0;

            if (indexA < 0 || indexB < 0)
            {
                // 如果采样点不在三角形内则回退
                centerWeight = 1;
            }
            else
            {
                var a = positionArray[indexA];
                var b = positionArray[indexB];

                // 使用重心坐标计算权重
                // （公式来自 http://en.wikipedia.org/wiki/Barycentric_coordinate_system_%28mathematics%29 ）
                float det = b.y * a.x - b.x * a.y; // 简化自：(b.y-0)*(a.x-0) + (0-b.x)*(a.y-0)；

                // TODO: 下面的 x 和 y 使用正确吗？
                float wA = (b.y * blendPosition.x - b.x * blendPosition.y) / det; // 简化自：((b.y-0)*(l.x-0) + (0-b.x)*(l.y-0)) / det;
                float wB = (a.x * blendPosition.y - a.y * blendPosition.x) / det; // 简化自：((0-a.y)*(l.x-0) + (a.x-0)*(l.y-0)) / det;
                centerWeight = 1 - wA - wB;

                // 夹住三角形内部
                if (centerWeight < 0)
                {
                    centerWeight = 0;
                    float sum = wA + wB;
                    wA /= sum;
                    wB /= sum;
                }
                else if (centerWeight > 1)
                {
                    centerWeight = 1;
                    wA = 0;
                    wB = 0;
                }

                // 将权重赋予最接近的外围两个顶点
                weightArray[indexA] = wA;
                weightArray[indexB] = wB;

                avgLen += clipLengthArray[indexA] * wA + clipLengthArray[indexB] * wB;
            }

            if (indexCenter >= 0)
            {
                weightArray[indexCenter] = centerWeight;
                avgLen += clipLengthArray[indexCenter] * centerWeight;
            }
            else
            {
                // 当输入位于中心时，赋予所有子级权重
                float sharedWeight = 1.0f / count;
                for (var i = 0; i < count; i++)
                {
                    weightArray[i] += sharedWeight * centerWeight;
                    avgLen += sharedWeight * centerWeight;
                }
            }
            return avgLen;
        }
    }

    [CreateAssetMenu(fileName = "Run", menuName = "NetCode/Animation/Run")]
    public class GhostAnimationGraph_Run : GhostAnimationGraphAsset
    {
        [Serializable]
        public struct BlendSpaceNode
        {
            public AnimationClip clip;
            public float2 position;
        }
        public List<BlendSpaceNode> BlendSpaceNodes;
        public AnimationClip AimClip;
        public override Playable CreatePlayable(GhostAnimationController controller, PlayableGraph graph, List<GhostPlayableBehaviour> behaviours)
        {
            var behaviourPlayable = ScriptPlayable<RunGhostPlayableBehaviour>.Create(graph);
            var behaviour = behaviourPlayable.GetBehaviour();
            // 这注册了接收 PreparePredictedData 的行为，如果 predicted 数据由 system 更新（PrepareFrame 仍然被调用），则跳过此操作
            behaviours.Add(behaviour);

            behaviour.Initialize(controller, graph, behaviourPlayable, BlendSpaceNodes, AimClip);
            return behaviourPlayable;
        }
        public override void RegisterPlayableData(IRegisterPlayableData register)
        {
            register.RegisterPlayableData<LocomotionAnimationData>();
        }
    }
#endif
}
