using Unity.Entities;
using Unity.Properties;
using UnityEngine;

namespace AutoAuthoring
{
    /// <summary>
    /// 计算类型中 Entity 字段的数量。它递归地遍历嵌套类型。
    /// </summary>
    class EntityFieldCountVisitor : IPropertyBagVisitor, IPropertyVisitor
    {
        int _EntityFieldCount = 0;

        /// <summary>
        /// 访问期间找到的 Entity 字段的数量。
        /// </summary>
        public int EntityFieldCount => _EntityFieldCount;

        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> propertyBag, ref TContainer container)
        {
            foreach (var property in propertyBag.GetProperties(ref container))
            {
                property.Accept(this, ref container);
            }
        }

        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            if (property is Property<TContainer, Entity> && !property.HasAttribute<HideInInspector>())
            {
                _EntityFieldCount++;
            }
            else
            {
                var value = property.GetValue(ref container);
                PropertyContainer.TryAccept(this, ref value);
            }
        }
    }

    /// <summary>
    /// 烘焙 GameObject 引用并在遍历的 component 的 Entity 字段上设置值。
    /// </summary>
    class ComponentDataPatcher : IPropertyBagVisitor, IPropertyVisitor
    {
        IBaker _Baker;
        GameObject[] _References;
        int _Index;

        public ComponentDataPatcher(IBaker baker, GameObject[] references = null)
        {
            _Baker = baker;
            Reset(references);
        }

        public void Reset(GameObject[] references)
        {
            _References = references;
            _Index = 0;
        }

        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> propertyBag, ref TContainer container)
        {
            foreach (var property in propertyBag.GetProperties(ref container))
            {
                property.Accept(this, ref container);
            }
        }

        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            var value = property.GetValue(ref container);
            if (property is Property<TContainer, Entity> entityProperty && !property.HasAttribute<HideInInspector>())
            {
                var reference = _References[_Index++];
                // TODO: 检查 TransformUsageFlags.Dynamic 在这种情况下是否正确
                var entity = _Baker.GetEntity(reference, TransformUsageFlags.Dynamic);

                entityProperty.SetValue(ref container, entity);
            }
            else
            {
                if (PropertyContainer.TryAccept(this, ref value))
                    property.SetValue(ref container, value);
            }
        }
    }
}
