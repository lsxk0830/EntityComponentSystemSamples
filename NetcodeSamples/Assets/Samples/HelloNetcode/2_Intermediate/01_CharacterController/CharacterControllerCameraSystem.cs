using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;

namespace Samples.HelloNetcode
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [BurstCompile]
    partial struct CharacterControllerCameraSystem : ISystem
    {
        public static readonly float3 k_CameraOffset = new float3(0, 2, -5);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnableCharacterController>();
            state.RequireForUpdate<NetworkStreamInGame>();
            state.RequireForUpdate<Character>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var camera = UnityEngine.Camera.main;
            //我们需要访问 LocalToWorld 矩阵来匹配玩家在演示中的位置。
            //因为 Physics 可以是 Interpolated 或 Predicted，所以 LocalToWorld 可以与真实的 world 位置不同
            //entity 的。
            foreach (var (localToWorld, input) in SystemAPI.Query<RefRO<LocalToWorld>, RefRO<CharacterControllerPlayerInput>>().WithAll<GhostOwnerIsLocal>())
            {
                camera.transform.rotation = math.mul(quaternion.RotateY(input.ValueRO.Yaw), quaternion.RotateX(-input.ValueRO.Pitch));
                var offset = math.rotate(camera.transform.rotation, k_CameraOffset);
                camera.transform.position = localToWorld.ValueRO.Position + offset;
            }
        }
    }
}
