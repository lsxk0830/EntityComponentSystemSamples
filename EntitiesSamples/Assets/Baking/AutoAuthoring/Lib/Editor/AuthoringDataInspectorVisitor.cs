using Unity.Entities;
using Unity.Properties;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AutoAuthoring
{
    /// <summary>
    /// 检查类型是否具有任何 Entity 字段。访问以嵌套类型递归完成。
    /// </summary>
    /// 当找到第一个 Entity 字段，或者遍历完所有 fields.</remarks> 后，<remarks>The 访问结束
    sealed class HasEntityFieldVisitor : IPropertyBagVisitor, IPropertyVisitor
    {
        public bool HasEntity;

        public void Reset()
        {
            HasEntity = false;
        }

        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> propertyBag, ref TContainer container)
        {
            foreach (var property in propertyBag.GetProperties(ref container))
            {
                if (property is Property<TContainer, Entity> && !property.HasAttribute<HideInInspector>())
                {
                    HasEntity = true;
                    break;
                }

                property.Accept(this, ref container);
            }
        }

        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            var value = property.GetValue(ref container);
            PropertyContainer.TryAccept(this, ref value);
        }
    }

    /// <summary>
    /// 构建可以在检查器中显示的 PropertyField 结构。
    /// </summary>
    sealed class PropertyFieldBuilderVisitor : IPropertyBagVisitor, IPropertyVisitor
    {
        VisualElement _Root;
        SerializedProperty _AuthoringDataProperty;
        SerializedProperty _ReferencesProperty;
        PropertyPath _PropertyPath;
        int _ReferenceIndex;
        HasEntityFieldVisitor _EntityVisitor;

        public PropertyFieldBuilderVisitor()
            : this(null, null, null) {}

        public PropertyFieldBuilderVisitor(VisualElement root, SerializedProperty authoringDataProperty, SerializedProperty referencesProperty)
        {
            _EntityVisitor = new HasEntityFieldVisitor();

            Reset(root, authoringDataProperty, referencesProperty);
        }

        public void Reset(VisualElement root, SerializedProperty authoringDataProperty, SerializedProperty referencesProperty)
        {
            _AuthoringDataProperty = authoringDataProperty;
            _ReferencesProperty = referencesProperty;
            _Root = root;
            _ReferenceIndex = 0;
        }

        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> propertyBag, ref TContainer container)
        {
            foreach (var property in propertyBag.GetProperties(ref container))
            {
                if (property.HasAttribute<HideInInspector>())
                    continue;

                property.Accept(this, ref container);
            }
        }

        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            var value = property.GetValue(ref container);

            if (property is Property<TContainer, Entity>)
            {
                // 将 Entity 字段重新映射到 GameObject 引用数组中的槽
                var pf = new PropertyField(_ReferencesProperty.GetArrayElementAtIndex(_ReferenceIndex++), ObjectNames.NicifyVariableName(property.Name));
                _Root.Add(pf);

                return;
            }

            _PropertyPath = PropertyPath.AppendProperty(_PropertyPath, property);

            if (!TypeTraits<TValue>.IsContainer)
            {
                var pf = new PropertyField(_AuthoringDataProperty.FindPropertyRelative(_PropertyPath.ToString()));
                _Root.Add(pf);
            }
            else
            {
                // FIXME: 在这里调用它使得算法为 N^2。搬到外面去
                _EntityVisitor.Reset();
                PropertyContainer.Accept(_EntityVisitor, property.GetValue(ref container));
                if (!_EntityVisitor.HasEntity)
                {
                    _Root.Add(new PropertyField(_AuthoringDataProperty.FindPropertyRelative(_PropertyPath.ToString())));
                }
                else
                {
                    var foldout = new Foldout() {text = ObjectNames.NicifyVariableName(property.Name)};
                    _Root.Add(foldout);
                    var currentRoot = _Root;
                    _Root = foldout;

                    PropertyContainer.Accept(this, ref value);

                    _Root = currentRoot;
                }
            }

            _PropertyPath = PropertyPath.Pop(_PropertyPath);
        }
    }
}
