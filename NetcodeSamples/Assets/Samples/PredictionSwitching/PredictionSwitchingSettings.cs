using Unity.Entities;

public struct PredictionSwitchingSettings : IComponentData
{
    public Entity Player;
    public float PlayerSpeed;

    public float TransitionDurationSeconds;
    public float PredictionSwitchingRadius;
    /// <summary>The 余量必须足够大，以便从 predicted 时间移动到 interpolated 时间不会将 ghost 移回到 prediction sphere.</summary>
    public float PredictionSwitchingMargin;

    public byte BallColorChangingEnabled;
}
