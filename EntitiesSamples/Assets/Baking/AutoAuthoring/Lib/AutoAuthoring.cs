using System;
using Unity.Entities;
using Unity.Properties;
using UnityEngine;

namespace AutoAuthoring
{
    /// <summary>
    /// 抑制复杂属性的顶层折叠
    /// </summary>
    public sealed class AutoAuthoringData : PropertyAttribute {}

    /// <summary>
    /// authoring components 的基类，默认为 bakers。
    /// </summary>
    public abstract class AutoAuthoringBase : MonoBehaviour
    {
        /// <summary>
        /// 解析 Entity 引用并烘焙 authoring 数据。
        /// 重写此方法以自定义 authoring 数据的 baking。
        /// </summary>
        /// <param name="baker">The baker instance.</param>
        internal abstract void Bake(IBaker baker);

        /// <summary>
        /// 要编写的 ECS component 的类型。
        /// </summary>
        /// <returns>Returns ECS component.</returns> 的类型
        public abstract Type GetComponentType();

        /// <summary>
        /// 如果 ECS component 是缓冲区类型，则为 true，否则为 false。
        /// </summary>
        public bool IsBufferComponent => typeof(IBufferElementData).IsAssignableFrom(GetComponentType());

        [BakeDerivedTypes]
        class Baker : Baker<AutoAuthoringBase>
        {
            public override void Bake(AutoAuthoringBase authoring)
            {
                authoring.Bake(this);
            }
        }
    }

    /// <summary>
    /// authoring components 的基类，专门用于每个 ECS component 类型。
    /// </summary>
    /// <typeparam name="TComponentData">The 型 ECS component.</typeparam>
    [DisallowMultipleComponent]
    public abstract class AutoAuthoringGeneric<TComponentData> : AutoAuthoringBase
        where TComponentData : new()
    {
        [Serializable]
        protected internal class ComponentDataInfo
        {
            public TComponentData AuthoringData;
            public GameObject[] References;
        }

        [Serializable]
        protected internal struct ComponentDataInfoArray
        {
            public ComponentDataInfo[] ComponentArray;
        }

        /// <summary>
        /// 序列化的 component 数据。
        /// </summary>
        /// <remarks>
        /// 使用属性 <see cref="AutoAuthoringData"/> 的自定义属性抽屉将数据反映在 UI 中。
        /// </remarks>
        [SerializeField]
        [AutoAuthoringData]
        protected internal ComponentDataInfoArray InfoArray;

        static int _EntityFieldCount = -1;

        /// <summary>
        /// ECS component 中找到的 entity 字段的数量。
        /// </summary>
        protected internal static int EntityFieldCount =>
            _EntityFieldCount = _EntityFieldCount == -1 ? ComputeEntityFieldCount() : _EntityFieldCount;

        /// <inheritdoc />
        public override Type GetComponentType() => typeof(TComponentData);

        void Reset()
        {
            OnValidate();
        }

        static int ComputeEntityFieldCount()
        {
            // 访问 component 类型的属性来统计 Entity 字段
            // Note: 对于每个实例化类型，在域重新加载后调用一次
            var visitor = new EntityFieldCountVisitor();
            PropertyContainer.Accept(visitor, new TComponentData());
            return visitor.EntityFieldCount;
        }

        void OnValidate()
        {
            // 初始化 GameObject 参考数组

            if (  (typeof(IComponentData).IsAssignableFrom(typeof(TComponentData))
                || typeof(ISharedComponentData).IsAssignableFrom(typeof(TComponentData))
                || typeof(ICleanupComponentData).IsAssignableFrom(typeof(TComponentData)))
                && InfoArray.ComponentArray is not {Length: 1})
            {
                InfoArray.ComponentArray = new ComponentDataInfo[] {new() {References = new GameObject[EntityFieldCount]}};
            }

            if (typeof(IBufferElementData).IsAssignableFrom(typeof(TComponentData))
                || typeof(ICleanupBufferElementData).IsAssignableFrom(typeof(TComponentData)))
            {
                if (InfoArray.ComponentArray == null)
                    InfoArray.ComponentArray = Array.Empty<ComponentDataInfo>();

                for (int i = 0, count = InfoArray.ComponentArray.Length; i < count; ++i)
                {
                    if (InfoArray.ComponentArray[i] == null)
                        continue;

                    ref var element = ref InfoArray.ComponentArray[i];
                    if (element.References == null || element.References.Length != EntityFieldCount)
                    {
                        element.References = new GameObject[EntityFieldCount];
                    }
                }
            }
        }
    }

    /// <summary>
    /// 提供默认的 baker 并支持 <see cref="IComponentData"/> ECS component 的 Entity 引用。
    /// </summary>
    /// <typeparam name="TComponentData">The ECS component，必须是非托管的并实现 <see cref="IComponentData"/>.</typeparam>
    public class AutoAuthoring<TComponentData> : AutoAuthoringGeneric<TComponentData>
        where TComponentData : unmanaged, IComponentData
    {
        /// <summary>
        /// authoring 数据实例。
        /// </summary>
        protected TComponentData Data => InfoArray.ComponentArray[0].AuthoringData;

        /// <inheritdoc />
        internal override void Bake(IBaker baker)
        {
            ref var info = ref InfoArray.ComponentArray[0];

            var visitor = new ComponentDataPatcher(baker, info.References);
            PropertyContainer.Accept(visitor, ref info.AuthoringData);
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent(entity, info.AuthoringData);
        }
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    /// <summary>
    /// 为 <see cref="IComponentData"/> ECS 管理的 component 提供默认的 baker 和对 Entity 引用的支持。
    /// </summary>
    /// <typeparam name="TComponentData">The ECS component，必须是 <b>class</b> 并实现 <see cref="IComponentData"/>.</typeparam>
    public class ManagedAutoAuthoring<TComponentData> : AutoAuthoringGeneric<TComponentData>
        where TComponentData : class, IComponentData, new()
    {
        /// <summary>
        /// authoring 数据实例。
        /// </summary>
        protected TComponentData Data => InfoArray.ComponentArray[0].AuthoringData;

        /// <inheritdoc />
        internal override void Bake(IBaker baker)
        {
            ref var info = ref InfoArray.ComponentArray[0];

            var visitor = new ComponentDataPatcher(baker, info.References);
            PropertyContainer.Accept(visitor, ref info.AuthoringData);
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponentObject(entity, info.AuthoringData);
        }
    }
#endif // !UNITY_DISABLE_MANAGED_COMPONENTS

    /// <summary>
    /// 提供默认的 baker 并支持 <see cref="IBufferElementData"/> ECS 缓冲区的 Entity 引用。
    /// </summary>
    /// <typeparam name="TComponentData">The ECS 缓冲区元素类型，必须是非托管的并实现 <see cref="IBufferElementData"/>.</typeparam>
    public class BufferAutoAuthoring<TComponentData> : AutoAuthoringGeneric<TComponentData>
        where TComponentData : unmanaged, IBufferElementData
    {
        /// <inheritdoc />
        internal override void Bake(IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            var buffer = baker.AddBuffer<TComponentData>(entity);
            var visitor = new ComponentDataPatcher(baker);
            for (int index = 0, count = InfoArray.ComponentArray?.Length ?? 0; index < count; ++index)
            {
                ref var element = ref InfoArray.ComponentArray[index];
                visitor.Reset(element.References);
                PropertyContainer.Accept(visitor, ref element.AuthoringData);
                buffer.Add(element.AuthoringData);
            }
        }
    }

    /// <summary>
    /// 提供默认的 baker 并支持 <see cref="ISharedComponentData"/> ECS component 的 Entity 引用。
    /// </summary>
    /// <typeparam name="TComponentData">The ECS component，必须是非托管的并实现 <see cref="ISharedComponentData"/>.</typeparam>
    public class SharedAutoAuthoring<TComponentData> : AutoAuthoringGeneric<TComponentData>
        where TComponentData : unmanaged, ISharedComponentData
    {
        /// <summary>
        /// authoring 数据实例。
        /// </summary>
        protected TComponentData Data => InfoArray.ComponentArray[0].AuthoringData;

        /// <inheritdoc />
        internal override void Bake(IBaker baker)
        {
            ref var info = ref InfoArray.ComponentArray[0];
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddSharedComponent(entity, info.AuthoringData);
        }
    }

    /// <summary>
    /// 为 <see cref="ISharedComponentData"/> ECS 管理的 component 提供默认的 baker 和对 Entity 引用的支持。
    /// </summary>
    /// <typeparam name="TComponentData">The ECS component，必须是 <b>struct</b> 并实现 <see cref="ISharedComponentData"/>.</typeparam>
    public class ManagedSharedAutoAuthoring<TComponentData> : AutoAuthoringGeneric<TComponentData>
        where TComponentData : struct, ISharedComponentData
    {
        /// <summary>
        /// authoring 数据实例。
        /// </summary>
        protected TComponentData Data => InfoArray.ComponentArray[0].AuthoringData;

        /// <inheritdoc />
        internal override void Bake(IBaker baker)
        {
            ref var info = ref InfoArray.ComponentArray[0];

            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddSharedComponentManaged(entity, info.AuthoringData);
        }
    }
}
