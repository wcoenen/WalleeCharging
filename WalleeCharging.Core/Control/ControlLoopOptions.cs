public class ControlLoopOptions
{
    /// <summary>
    /// Delay between control loop iterations in milliseconds.
    /// </summary>
    public int LoopDelayMillis { get; set; } = 5000;

    /// <summary>
    /// Maximum safe current in Ampere that can be drawn through the main meter.
    /// </summary>
    public int MaxSafeCurrentAmpere { get; set; } = 16;

    /// <summary>
    /// If true, the control loop will simulate actions without actually controlling the charging station.
    /// </summary>
    public bool ShadowMode { get; set; }
}