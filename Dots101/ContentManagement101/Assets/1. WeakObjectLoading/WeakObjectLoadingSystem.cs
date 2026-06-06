using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ContentManagement.Sample
{
    // 此 system 查找具有由 WeakRenderedObjectAuthoring 添加的 components 的 entities
    // 并通过以下方式将它们转换为可渲染的 entities：
    // 1. 异步加载弱引用的网格和材质
    // 2. 资源加载后，添加所需的渲染 components
    public partial struct WeakObjectLoadingSystem : ISystem
    {
        private EntityQuery weakQuery;
        private EntityQuery weakUntypedQuery;

        public void OnCreate(ref SystemState state)
        {
            // 可渲染的 entities 具有 RenderBounds component（以及其他渲染 components），因此
            // 这些查询仅匹配尚未可渲染的 entities。
            weakQuery = SystemAPI.QueryBuilder().WithAll<WeakMesh, WeakMaterial>().WithNone<RenderBounds>().Build();
            weakUntypedQuery = SystemAPI.QueryBuilder().WithAll<WeakMeshUntyped, WeakMaterialUntyped>()
                .WithNone<RenderBounds>().Build();

            // 仅当 entities 存在且仍需要可渲染时，system 才应更新。
            var query = SystemAPI.QueryBuilder().WithAny<WeakMesh, WeakMeshUntyped>().WithNone<RenderBounds>().Build();
            state.RequireForUpdate(query);
        }

        public void OnUpdate(ref SystemState state)
        {
#region WeakObjectReference
            var weakEntities = weakQuery.ToEntityArray(Allocator.Temp);
            var weakMeshes = weakQuery.ToComponentDataArray<WeakMesh>(Allocator.Temp);

            // note 我们不能在这个循环中使用 SystemAPI.Query 因为
            // 正在对 entities 进行结构性更改
            for (int i = 0; i < weakEntities.Length; i++)
            {
                var loaded = true;

                var entity = weakEntities[i];
                var mesh = weakMeshes[i];
                var materials = state.EntityManager.GetBuffer<WeakMaterial>(entity);

                // 网格负载状态
                var meshStatus = mesh.Value.LoadingStatus;
                if (meshStatus == ObjectLoadingStatus.None)
                {
                    Debug.Log("Initiate mesh LOAD");
                    mesh.Value.LoadAsync(); // trigger 负载
                }

                if (meshStatus != ObjectLoadingStatus.Completed)
                {
                    loaded = false;
                }

                // 物料装载状态
                for (int j = 0; j < materials.Length; j++)
                {
                    var mat = materials[j];
                    var materialStatus = mat.Value.LoadingStatus;
                    if (materialStatus == ObjectLoadingStatus.None)
                    {
                        Debug.Log("Initiate material LOAD");
                        mat.Value.LoadAsync(); // trigger 负载
                    }

                    if (materialStatus != ObjectLoadingStatus.Completed)
                    {
                        loaded = false;
                    }
                }

                if (loaded)
                {
                    Debug.Log("Creating rendered entity");

                    // 每个渲染的对象都有一个网格，但是网格可以
                    // 有子网格，每个子网格都有自己的材质
                    var meshArray = new Mesh[] { mesh.Value.Result };
                    var materialArray = new Material[materials.Length];
                    var indices = new MaterialMeshIndex[materials.Length];

                    for (int j = 0; j < materials.Length; j++)
                    {
                        materialArray[j] = materials[j].Value.Result;
                        indices[j] = new MaterialMeshIndex
                        {
                            MeshIndex = 0,
                            MaterialIndex = j,
                            SubMeshIndex = j,
                        };
                    }

                    // 将渲染 components 添加到 entity
                    // （包括 RenderBounds，因此 entity 此后将
                    // 被排除在此 system 的查询之外）
                    RenderMeshUtility.AddComponents(entity, state.EntityManager,
                        new RenderMeshDescription(ShadowCastingMode.On),
                        new RenderMeshArray(materialArray, meshArray, indices),
                        MaterialMeshInfo.FromMaterialMeshIndexRange(0, materialArray.Length)
                    );
                }
            }
#endregion

#region UntypedWeakReferenceId
            // 与上面非常相似，但对于 UntypedWeakReferenceId...

            var weakUntypedEntities = weakUntypedQuery.ToEntityArray(Allocator.Temp);
            var untypedWeakMeshes = weakUntypedQuery.ToComponentDataArray<WeakMeshUntyped>(Allocator.Temp);

            for (int i = 0; i < weakUntypedEntities.Length; i++)
            {
                var loaded = true;

                var entity = weakUntypedEntities[i];
                var mesh = untypedWeakMeshes[i];
                var materials = state.EntityManager.GetBuffer<WeakMaterialUntyped>(entity);

                // 网格负载状态
                var meshStatus = RuntimeContentManager.GetObjectLoadingStatus(mesh.Value);
                if (meshStatus == ObjectLoadingStatus.None)
                {
                    RuntimeContentManager.LoadObjectAsync(mesh.Value);  // trigger 负载
                }
                if (meshStatus != ObjectLoadingStatus.Completed)
                {
                    loaded = false;
                }

                // 物料装载状态
                for (int j = 0; j < materials.Length; j++)
                {
                    var mat = materials[j];
                    var materialStatus = RuntimeContentManager.GetObjectLoadingStatus(mat.Value);
                    if (materialStatus == ObjectLoadingStatus.None)
                    {
                        RuntimeContentManager.LoadObjectAsync(mat.Value);  // trigger 负载
                    }

                    if (materialStatus != ObjectLoadingStatus.Completed)
                    {
                        loaded = false;
                    }
                }

                if (loaded)
                {
                    // 每个渲染的对象都有一个网格，但是网格可以
                    // 有子网格，每个子网格都有自己的材质
                    var meshArray = new Mesh[] { RuntimeContentManager.GetObjectValue<Mesh>(mesh.Value) };
                    var materialArray = new Material[materials.Length];
                    var indices = new MaterialMeshIndex[materials.Length];

                    for (int j = 0; j < materials.Length; j++)
                    {
                        materialArray[j] = RuntimeContentManager.GetObjectValue<Material>(materials[j].Value);
                        indices[j] = new MaterialMeshIndex
                        {
                            MeshIndex = 0,
                            MaterialIndex = j,
                            SubMeshIndex = j,
                        };
                    }

                    // 将渲染 components 添加到 entity
                    // （包括 RenderBounds，因此 entity 将从此 system 的查询中排除）
                    RenderMeshUtility.AddComponents(entity, state.EntityManager,
                        new RenderMeshDescription(ShadowCastingMode.On),
                        new RenderMeshArray(materialArray, meshArray, indices),
                        MaterialMeshInfo.FromMaterialMeshIndexRange(0, materialArray.Length)
                    );
                }
            }
#endregion
        }
    }
}