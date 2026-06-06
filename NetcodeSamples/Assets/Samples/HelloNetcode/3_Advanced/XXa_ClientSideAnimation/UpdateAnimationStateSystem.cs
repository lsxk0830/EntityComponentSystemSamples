#if !UNITY_DISABLE_MANAGED_COMPONENTS
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using Unity.Transforms;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// 在收集输入之后但在调用 Animator.Update 之前运行此 system。
    /// 由于 Animator.Update 在 SimulationSystemGroup 之后立即调用，
    /// 我们在 system 组运行后立即注入自己。
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct UpdateAnimationStateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnableAnimation>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var presentationGameObjectSystem =
                state.World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            foreach (var (input, localToWorld, character, entity) in SystemAPI
                         .Query<RefRO<CharacterControllerPlayerInput>, RefRW<LocalTransform>, RefRO<Character>>()
                         .WithAll<GhostOwnerIsLocal>()
                         .WithEntityAccess())
            {
                var gameObjectForEntity =
                    presentationGameObjectSystem.GetGameObjectForEntity(state.EntityManager, entity);
                var myData = new CharacterAnimationData
                {
                    IsShooting = input.ValueRO.PrimaryFire.IsSet,
                    Movement = input.ValueRO.Movement,
                    OnGround = character.ValueRO.OnGround == 1,
                    Pitch = input.ValueRO.Pitch,
                    Yaw = input.ValueRO.Yaw,
                };
                var characterAnimation = gameObjectForEntity.GetComponent<CharacterAnimation>();
                if (characterAnimation != null)
                {
                    localToWorld.ValueRW = characterAnimation.UpdateAnimationState(myData, localToWorld.ValueRO);
                }
            }
        }
    }
}
#endif
