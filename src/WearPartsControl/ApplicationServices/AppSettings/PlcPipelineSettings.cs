namespace WearPartsControl.ApplicationServices.AppSettings;

public sealed class PlcPipelineSettings
{
    /// <summary>
    /// PLC 管线排队等待慢调用阈值，单位毫秒。
    /// </summary>
    public int SlowQueueWaitThresholdMilliseconds { get; set; } = 100;

    /// <summary>
    /// PLC 管线执行慢调用阈值，单位毫秒。
    /// </summary>
    public int SlowExecutionThresholdMilliseconds { get; set; } = 500;
}