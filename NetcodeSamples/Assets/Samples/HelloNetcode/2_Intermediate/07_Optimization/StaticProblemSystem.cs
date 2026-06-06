using Unity.Entities;
using Unity.Transforms;

namespace Samples.HelloNetcode
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(HelloNetcodeSystemGroup))]
    [UpdateAfter(typeof(BarrelSpawnerSystem))]
    public partial class StaticProblemSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<EnableOptimization>();
        }

        protected override void OnUpdate()
        {
            var setup = SystemAPI.GetSingleton<BarrelSetup>();
            if (!setup.EnableStaticOptimizationProblem)
            {
                Enabled = false;
            }


            /* 这是故意不正确的。我们获取对转换数据的写访问权，但从不修改它。 */
            foreach (var trans in SystemAPI.Query<RefRW<LocalTransform>>())
            {

            }
        }
    }
}
