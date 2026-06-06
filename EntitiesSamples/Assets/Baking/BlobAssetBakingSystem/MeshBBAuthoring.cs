using UnityEngine;
using UnityObject = UnityEngine.Object;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using Hash128 = Unity.Entities.Hash128;

namespace Baking.BlobAssetBakingSystem
{
#if UNITY_EDITOR
    public class MeshBBAuthoring : MonoBehaviour
    {
        public float MeshScale = 1;

        class Baker : Baker<MeshBBAuthoring>
        {
            public override void Bake(MeshBBAuthoring authoring)
            {
                var meshVertices = new NativeList<MeshVertex>(Allocator.Temp);
                var vertices = new List<Vector3>(4096);

                // 根据 Authoring 属性计算 blob 资产哈希
                var mesh = GetComponent<MeshFilter>().sharedMesh;
                DependsOn(mesh);

                var hasMesh = mesh != null;
                var assetPath = AssetDatabase.GetAssetPath(mesh);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh.GetInstanceID(), out string guid, out long localId);
                Hash128 hash = default;

                // 在这里，我们从网格中获取哈希值，以便稍后使用它来删除重复的 blob 资源
                // mesh.GetHashCode() will not update if the mesh is changed outside of the editor
                // 所以我们通过资产数据库从资产路径中获取哈希值。
                // 但是，尝试从默认资源（例如立方体、胶囊）获取哈希值将始终给出 0 的哈希值
                // 另外，它不会改变（除了 Unity 版本更新），所以我们可以使用 GetHashCode
                if (IsBuiltin(new GUID(guid)))
                {
                    hash = new Hash128((uint) localId, (uint) authoring.MeshScale.GetHashCode(), 0, 0);
                }
                else if (UnityEditor.EditorUtility.IsPersistent(mesh))
                {
                    hash = AssetDatabase.GetAssetDependencyHash(assetPath);
                }
                else
                {
                    Debug.LogError("This sample does not support procedural meshes stored in the scene");
                }

                // 将网格顶点复制到动态缓冲区数组中
                if (hasMesh)
                {
                    mesh.GetVertices(vertices);
                    for (int i = 0; i < vertices.Count; i++)
                    {
                        var p = vertices[i];
                        meshVertices.Add(new MeshVertex() {Value = p});
                    }
                }

                // 添加带有顶点的动态缓冲区以在 BakingSystem 中创建 BlobAsset
                var entity = GetEntity(TransformUsageFlags.None);
                var buffer = AddBuffer<MeshVertex>(entity);
                buffer.AddRange(meshVertices.AsArray());

                // 添加哈希和缩放以进行 BlobAsset 创建
                AddComponent(entity, new RawMesh()
                {
                    MeshScale = authoring.MeshScale,
                    Hash = hash
                });

                // 添加稍后将保存 BlobAssetReference 的 Component
                AddComponent(entity, new MeshBB());
            }

            public static GUID UnityEditorResources = new GUID("0000000000000000d000000000000000");
            public static GUID UnityBuiltinResources = new GUID("0000000000000000e000000000000000");
            public static GUID UnityBuiltinExtraResources = new GUID("0000000000000000f000000000000000");

            public static bool IsBuiltin(in GUID g) =>
                g == UnityEditorResources ||
                g == UnityBuiltinResources ||
                g == UnityBuiltinExtraResources;
        }
    }
#endif

    public struct MeshBBBlobAsset
    {
        public float3 MinBoundingBox;
        public float3 MaxBoundingBox;
    }

    public struct MeshBB : IComponentData
    {
        public BlobAssetReference<MeshBBBlobAsset> BlobData;
    }

    [BakingType]
    public struct RawMesh : IComponentData
    {
        public float MeshScale;
        public Hash128 Hash;
    }

    [BakingType]
    public struct MeshVertex : IBufferElementData
    {
        public float3 Value;
    }
}
