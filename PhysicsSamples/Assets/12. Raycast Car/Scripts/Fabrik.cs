//来自：https://github.com/stonneau/fabrik

using System;
using System.Collections.Generic;
using UnityEngine;

public class Fabrik : MonoBehaviour
{
    /*维持骨骼关节之间关系的恒定对象
     * 以深度优先的顺序迭代变换。
     * */
    public class JointInfo
    {
        public readonly float distanceToParent_; // 如果为 0，则没有父级
        public readonly float distanceToRoot_; // 如果为 0，则没有父级
        public readonly int id_; // 唯一 ID
        public readonly JointInfo parent_;
        public readonly JointInfo fork_; // 有几个孩子的最亲近的父母
        public readonly JointInfo[] children_;
        public readonly JointInfo[] effectors_; // 与从“this”开始的链相关的效应器

        public JointInfo(Transform joint, ref int id) : this(joint, ref id, null, null)
        {
            // NOTHING
        }

        private JointInfo(Transform joint, ref int id, JointInfo parent, JointInfo fork)
        {
            id_ = id;
            fork_ = fork;
            parent_ = parent;
            if (parent == null)
            {
                distanceToParent_ = 0;
                distanceToRoot_ = 0;
            }
            else
            {
                distanceToParent_ = Vector3.Distance(joint.parent.position, joint.position);
                distanceToRoot_ = parent.distanceToRoot_ + distanceToParent_;
            }
            int nbChildren = joint.childCount;
            children_ = new JointInfo[nbChildren];
            if (nbChildren == 0) // joint 是效应器
            {
                effectors_ = new JointInfo[1];
                effectors_[0] = this;
            }
            else
            {
                int nbEffectors_ = 0;
                JointInfo childFork = fork_;
                if (nbChildren > 1)
                {
                    childFork = this;
                }
                for (int i = 0; i < nbChildren; ++i)
                {
                    // id 通过深度优先迭代更新
                    ++id;
                    JointInfo jInfo = new JointInfo(joint.GetChild(i), ref id, this, childFork);
                    children_[i] = jInfo;
                    nbEffectors_ += jInfo.effectors_.Length;
                }
                effectors_ = new JointInfo[nbEffectors_];
                int j = 0;
                foreach (JointInfo jInfo in children_)
                {
                    foreach (JointInfo effector in jInfo.effectors_)
                    {
                        effectors_[j++] = effector;
                    }
                }
            }
        }

        public bool Equals(JointInfo jInfo)
        {
            return jInfo != null && id_ == jInfo.id_;
        }
    }

    // 暴露的属性
    public Transform ikChain; // 将在其上执行 Ik 的运动链
    public Transform[] targets; // 有序的目标列表
                                /*public float treshold;*/
                                // 结束暴露的属性

    private JointInfo jointInfo_; // 持续的
    private Transform[] transforms_; // 与我们的链相关的转换

    void Start()
    {
        int id = 0;
        jointInfo_ = new JointInfo(ikChain, ref id);
        transforms_ = new Transform[id + 1];
        id = 0;
        InitTransform(ikChain, ref id);
    }

    // 兴趣指数变换
    private void InitTransform(Transform transform, ref int id)
    {
        transforms_[id] = transform;
        for (int i = 0; i < transform.childCount; ++i)
        {
            ++id;
            InitTransform(transform.GetChild(i), ref id);
        }
    }

    Vector3[] m_TargetPositions = new Vector3[0];

    unsafe void Update()
    {
        Vector3 rootPos = ikChain.position;
        if (m_TargetPositions.Length != targets.Length)
            Array.Resize(ref m_TargetPositions, targets.Length);
        for (var i = 0; i < m_TargetPositions.Length; ++i)
            m_TargetPositions[i] = targets[i].position;
        ForwardStep(jointInfo_.effectors_, m_TargetPositions);
        ikChain.position = rootPos;
        BackwardStep(jointInfo_);
    }

    /*
     * 允许计算不同点之间质心的结构
     * TODO : 目标优先顺序?
     */
    private class TargetCentroid
    {
        private int nbMatches_;
        private Vector3 target_;
        public TargetCentroid()
        {
            nbMatches_ = 0;
            target_ = new Vector3(0, 0, 0);
        }

        public void addTarget(Vector3 target)
        {
            target_ += target;
            ++nbMatches_;
        }

        public Vector3 Target
        {
            get { return target_ / Mathf.Max(nbMatches_, 1); }
        }
    }

    // 第一步：从每个末端执行器到最近的货叉
    // 在分叉处，确定目标的质心位置
    // 然后走到下一个岔路口
    private void ForwardStep(JointInfo[] effectors, Vector3[] targets)
    {
        Dictionary<JointInfo, TargetCentroid> centroids = new Dictionary<JointInfo, TargetCentroid>();
        for (int i = 0; i < effectors.Length; ++i)
        {
            JointInfo effector = effectors[i];
            Vector3 target = targets[i];
            transforms_[effector.id_].position = target;
            Vector3 forkTarget = ForwardStepJoint(effector);
            JointInfo fork = effector.fork_;
            if (fork != null)
            {
                if (!centroids.ContainsKey(fork))
                {
                    centroids[fork] = new TargetCentroid();
                }
                centroids[fork].addTarget(forkTarget);
            }
        }
        int nbCentroids = centroids.Count;
        if (nbCentroids > 0)
        {
            JointInfo[] upEffectors = new JointInfo[nbCentroids];
            Vector3[] upTargets = new Vector3[nbCentroids];
            int j = 0;
            foreach (KeyValuePair<JointInfo, TargetCentroid> pair in centroids)
            {
                upEffectors[j] = pair.Key;
                upTargets[j] = pair.Value.Target;
                ++j;
            }
            ForwardStep(upEffectors, upTargets);
        }
    }

    private Vector3 ForwardStepJoint(JointInfo jointInfo)
    {
        if (jointInfo.parent_ != null)
        {
            Transform transform = transforms_[jointInfo.id_];
            Transform parentTransform = transforms_[jointInfo.parent_.id_];
            float r = Vector3.Distance(transform.position, parentTransform.position);
            float delta = jointInfo.distanceToParent_ / r;
            Vector3 newPos = (1 - delta) * transform.position + delta * parentTransform.position;
            if (jointInfo.parent_.Equals(jointInfo.fork_)) // 父级是 fork，不要修改位置，我们将取质心
            {
                return newPos;
            }

            MoveTransformToPosition(jointInfo.parent_, newPos);
            return ForwardStepJoint(jointInfo.parent_);
        }

        return ikChain.position;
    }

    private void BackwardStep(JointInfo jointInfo)
    {
        if (jointInfo.children_.Length != 0)
        {
            Transform transform = transforms_[jointInfo.id_];
            foreach (JointInfo childInfo in jointInfo.children_)
            {
                Transform childTransform = transforms_[childInfo.id_];
                float r = Vector3.Distance(transform.position, childTransform.position);
                float delta = childInfo.distanceToParent_ / r;
                Vector3 newPos = (1 - delta) * transform.position + delta * childTransform.position;
                MoveTransformToPosition(childInfo, newPos);
                BackwardStep(childInfo);
            }
        }
    }

    private void MoveTransformToPosition(JointInfo jointInfo, Vector3 target)
    {
        Transform transform = transforms_[jointInfo.id_];
        if (jointInfo.parent_ != null) transform.position = target;
    }
}
