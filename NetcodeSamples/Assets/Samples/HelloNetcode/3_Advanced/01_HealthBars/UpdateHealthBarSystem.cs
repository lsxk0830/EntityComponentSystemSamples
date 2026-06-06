using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

namespace Samples.HelloNetcode
{
#if !UNITY_DISABLE_MANAGED_COMPONENTS
    /// <summary>
    /// 更新玩家上方生命条的位置和旋转。这将确保健康栏跟随角色
    /// 角色并且始终面向主摄像机。
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct UpdateHealthBarSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (Camera.main == null)
            {
                state.Enabled = false;
                return;
            }

            var mainCamera = Camera.main;

            foreach (var (ui, health, act, owner, ltw, entity) in SystemAPI.Query<HealthUI, RefRO<Health>, RefRO<AutoCommandTarget>, RefRO<GhostOwner>, RefRO<LocalToWorld>>().WithEntityAccess())
            {

                if (state.EntityManager.IsComponentEnabled<GhostOwnerIsLocal>(entity))
                {
                    // 将 UI 移动到本地播放器的顶部，以便您可以看到。
                    var targetHealthBarPos = ltw.ValueRO.Position;
                    targetHealthBarPos.y += ui.PlayerHeightOffset;
                    var n = mainCamera.transform.position - ui.HealthBar.position;
                    targetHealthBarPos += (float3)(n.normalized * ui.PlayerTowardCameraOffset);
                    ui.HealthBar.SetPositionAndRotation(targetHealthBarPos, Quaternion.LookRotation(n));
                }
                else
                {
                    // 将 UI 移至玩家头顶上方。
                    var targetHealthBarPos = ltw.ValueRO.Position;
                    targetHealthBarPos.y += ui.OpponentHeightOffset;
                    var n = mainCamera.transform.position - ui.HealthBar.position;
                    ui.HealthBar.SetPositionAndRotation(targetHealthBarPos, Quaternion.LookRotation(n));
                }

                var hpNormalized = math.saturate((float)health.ValueRO.CurrentHitPoints / health.ValueRO.MaximumHitPoints);
                var playerColor = NetworkIdDebugColorUtility.GetColor(owner.ValueRO.NetworkId);

                // 被 server 杀死：
                if (act.ValueRO.Enabled)
                {
                    // 设置为玩家颜色：
                    ui.HealthSlider.color = playerColor;
                }
                else
                {
                    // 无论 prediction 都设置为 0，并将背景更改为权威死者。
                    // Note 我们只有在 AutoCommandTarget 设置后才执行此操作。为什么？我们正在等待 server 确认。
                    hpNormalized = 0;
                    playerColor.a = 0.3f;
                    ui.HealthSlider.transform.parent.GetComponent<Image>().color = playerColor;
                }

                ui.HealthSlider.fillAmount = hpNormalized;
            }

        }
    }
#endif
}
