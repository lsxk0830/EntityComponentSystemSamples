using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Tutorials.Firefighters
{
    public partial struct BotSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Config>();
            state.RequireForUpdate<Heat>();
            state.RequireForUpdate<ExecuteBot>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Config>();
            var dt = SystemAPI.Time.DeltaTime;
            var moveSpeed = dt * config.BotMoveSpeed;
            var fillRate = dt * config.BucketFillRate;

            var heatBuffer = SystemAPI.GetSingletonBuffer<Heat>();

            BotUpdate_MainThread(ref state, heatBuffer, moveSpeed, fillRate, config.GroundNumRows, config.GroundNumColumns);
        }

        private void BotUpdate_MainThread(ref SystemState state, DynamicBuffer<Heat> heatBuffer, float moveSpeed,
            float fillRate, int numRows,
            int numCols)
        {
            foreach (var (bot, botTrans, botEntity) in
                     SystemAPI.Query<RefRW<Bot>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                switch (bot.ValueRO.State)
                {
                    case BotState.MOVE_TO_BUCKET:
                    {
                        if (MoveToTarget(ref botTrans.ValueRW, bot.ValueRO.TargetPos, moveSpeed))
                        {
                            var team = SystemAPI.GetComponent<Team>(bot.ValueRO.Team);

                            var bucket = SystemAPI.GetComponentRW<Bucket>(team.Bucket);
                            bucket.ValueRW.CarryingBot = botEntity;
                            bucket.ValueRW.IsCarried = true;

                            bot.ValueRW.Bucket = team.Bucket;
                            bot.ValueRW.IsCarrying = true;
                            bot.ValueRW.TargetPos = bot.ValueRO.LinePos; // 设置于 TeamSystem
                            bot.ValueRW.State = BotState.MOVE_TO_LINE;
                        }

                        break;
                    }
                    case BotState.FILL_BUCKET:
                    {
                        var bucket = SystemAPI.GetComponentRW<Bucket>(bot.ValueRO.Bucket);
                        var val = fillRate + bucket.ValueRO.Water;
                        if (val < 1.0f) // 继续填充
                        {
                            bucket.ValueRW.Water = fillRate + bucket.ValueRO.Water;
                        }
                        else // 完成填充
                        {
                            bucket.ValueRW.Water = 1;
                            bot.ValueRW.State = BotState.PASS_BUCKET;
                        }

                        break;
                    }
                    case BotState.DOUSE_FIRE:
                    {
                        // （只有遮光板应处于此状态）
                        if (MoveToTarget(ref botTrans.ValueRW, bot.ValueRO.TargetPos, moveSpeed))
                        {
                            var bucket = SystemAPI.GetComponentRW<Bucket>(bot.ValueRO.Bucket);
                            bucket.ValueRW.Water = 0;

                            HeatSystem.DouseFire(bot.ValueRO.TargetPos, heatBuffer, numRows, numCols);

                            SystemAPI.SetComponentEnabled<RepositionLine>(bot.ValueRO.Team, true);
                            var team = SystemAPI.GetComponentRW<Team>(bot.ValueRO.Team);
                            team.ValueRW.NumFiresDoused++;

                            bot.ValueRW.State = BotState.MOVE_TO_LINE;
                        }

                        break;
                    }
                    case BotState.PASS_BUCKET:
                    {
                        // 当我们传递给下一个机器人时，它通常不应该移动，但为了以防万一，我们得到了它当前的位置
                        var targetPos = SystemAPI.GetComponent<LocalTransform>(bot.ValueRO.NextBot).Position.xz;
                        if (MoveToTarget(ref botTrans.ValueRW, targetPos, moveSpeed))
                        {
                            var bucket = SystemAPI.GetComponentRW<Bucket>(bot.ValueRO.Bucket);
                            bucket.ValueRW.CarryingBot = bot.ValueRO.NextBot;

                            var otherBot = SystemAPI.GetComponentRW<Bot>(bot.ValueRO.NextBot);
                            otherBot.ValueRW.IsCarrying = true;
                            otherBot.ValueRW.Bucket = bot.ValueRO.Bucket;

                            bot.ValueRW.IsCarrying = false;
                            bot.ValueRW.Bucket = Entity.Null;
                            bot.ValueRW.State = BotState.MOVE_TO_LINE;
                        }

                        break;
                    }
                    case BotState.MOVE_TO_LINE:
                    {
                        if (MoveToTarget(ref botTrans.ValueRW, bot.ValueRO.LinePos, moveSpeed))
                        {
                            bot.ValueRW.State = BotState.WAIT_IN_LINE;
                        }

                        break;
                    }
                    case BotState.WAIT_IN_LINE:
                    {
                        if (!bot.ValueRO.IsCarrying)
                        {
                            break;
                        }

                        if (bot.ValueRO.IsFiller)
                        {
                            var bucket = SystemAPI.GetComponentRW<Bucket>(bot.ValueRO.Bucket);
                            if (bucket.ValueRO.Water < 1)
                            {
                                bot.ValueRW.State = BotState.FILL_BUCKET;
                            }
                            else // 满桶
                            {
                                bot.ValueRW.State = BotState.PASS_BUCKET;
                            }

                            break;
                        }

                        if (bot.ValueRO.IsDouser)
                        {
                            var bucket = SystemAPI.GetComponentRW<Bucket>(bot.ValueRO.Bucket);
                            if (bucket.ValueRO.Water > 0)
                            {
                                bot.ValueRW.State = BotState.DOUSE_FIRE;
                            }
                            else // 空桶
                            {
                                bot.ValueRW.State = BotState.PASS_BUCKET;
                            }

                            break;
                        }

                        bot.ValueRW.State = BotState.PASS_BUCKET;
                        break;
                    }
                }
            }
        }

        // return true if advanced up to or past the target pos
        private static bool MoveToTarget(ref LocalTransform botTrans, float2 targetPos, float moveSpeed)
        {
            var pos = botTrans.Position;
            var dir = targetPos - pos.xz;
            var moveVectorNormalized = math.normalizesafe(dir);
            var moveVector = moveVectorNormalized * moveSpeed;

            // 动画模型面向 z 轴，因此我们需要将其顺时针旋转 90 度。
            var modelRotation = math.radians(90);

            // atan2 返回逆时针旋转角度，因此我们求反使其顺时针旋转
            var facingRotation = -math.atan2(moveVectorNormalized.y, moveVectorNormalized.x);

            botTrans.Rotation = quaternion.RotateY(modelRotation + facingRotation);

            if (math.lengthsq(moveVector) >= math.lengthsq(dir))
            {
                botTrans.Position.x = targetPos.x;
                botTrans.Position.z = targetPos.y;
                return true;
            }

            botTrans.Position += new float3(moveVector.x, 0, moveVector.y);
            return false;
        }
    }
}
