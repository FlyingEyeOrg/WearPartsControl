using Microsoft.Extensions.Logging;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.MonitoringLogs;
using WearPartsControl.ApplicationServices.PlcService;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class PlcOperationPipelineTests
{
    [Fact]
    public async Task ConnectAsync_WhenCalledConcurrently_ShouldSerializeOperations()
    {
        var plcService = new PipelineTestPlcService();
        var logger = new TestLogger<PlcOperationPipeline>();
        var pipeline = new PlcOperationPipeline(plcService, logger);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = false;
        var executionOrder = new List<string>();

        plcService.OnConnectAsync = async (options, _) =>
        {
            if (options.IpAddress == "ADDR-1")
            {
                executionOrder.Add("first-start");
                firstEntered.SetResult();
                await releaseFirst.Task;
                executionOrder.Add("first-end");
                return;
            }

            secondStarted = true;
            executionOrder.Add("second");
            await Task.CompletedTask;
        };

        var firstTask = pipeline.ConnectAsync("Test/First", new PlcConnectionOptions { IpAddress = "ADDR-1" });

        await firstEntered.Task;

        var secondTask = pipeline.ConnectAsync("Test/Second", new PlcConnectionOptions { IpAddress = "ADDR-2" });

        await Task.Delay(100);
        Assert.False(secondStarted);

        releaseFirst.SetResult();
        await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(new[] { "first-start", "first-end", "second" }, executionOrder);
    }

    [Fact]
    public async Task ReadAsync_WhenOperationIsSlow_ShouldWriteWarningLog()
    {
        var plcService = new PipelineTestPlcService();
        var logger = new TestLogger<PlcOperationPipeline>();
        var pipeline = new PlcOperationPipeline(plcService, logger);

        plcService.OnRead = _ =>
        {
            Thread.Sleep(550);
            return 1;
        };

        _ = await pipeline.ReadAsync<int>("Test/SlowOperation", "ADDR-SLOW");

        Assert.Contains(logger.Entries, entry =>
            entry.LogLevel == LogLevel.Warning
            && entry.Message.Contains("Test/SlowOperation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadAsync_WhenThresholdIsConfigured_ShouldUseConfiguredValue()
    {
        var plcService = new PipelineTestPlcService();
        var logger = new TestLogger<PlcOperationPipeline>();
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                PlcPipeline = new PlcPipelineSettings
                {
                    SlowQueueWaitThresholdMilliseconds = 100,
                    SlowExecutionThresholdMilliseconds = 50
                }
            }
        };
        var pipeline = new PlcOperationPipeline(plcService, logger, appSettingsService);

        plcService.OnRead = _ =>
        {
            Thread.Sleep(80);
            return 1;
        };

        _ = await pipeline.ReadAsync<int>("Test/ConfiguredSlowOperation", "ADDR-SLOW");

        Assert.Contains(logger.Entries, entry =>
            entry.LogLevel == LogLevel.Warning
            && entry.Message.Contains("Test/ConfiguredSlowOperation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadAsync_WhenCalledFromThread_ShouldExecutePlcOperationOnWorkerThread()
    {
        var callerThreadId = Environment.CurrentManagedThreadId;
        var operationThreadId = callerThreadId;
        var plcService = new PipelineTestPlcService();
        var logger = new TestLogger<PlcOperationPipeline>();
        var pipeline = new PlcOperationPipeline(plcService, logger);

        plcService.OnRead = _ =>
        {
            operationThreadId = Environment.CurrentManagedThreadId;
            return 1;
        };

        _ = await pipeline.ReadAsync<int>("Test/BackgroundRead", "ADDR-1");

        Assert.NotEqual(callerThreadId, operationThreadId);
    }

    [Fact]
    public async Task ReadAsync_WhenMonitoringOperationSucceeds_ShouldPublishUiLogEntry()
    {
        var plcService = new PipelineTestPlcService();
        var logger = new TestLogger<PlcOperationPipeline>();
        var monitoringLogPipeline = new WearPartMonitoringLogPipeline();
        var pipeline = new PlcOperationPipeline(plcService, logger, monitoringLogPipeline: monitoringLogPipeline);

        plcService.OnRead = _ => 42;

        _ = await pipeline.ReadAsync<int>(PlcMonitoringPipelineOperations.ReadCurrentValue, "DB1.0");
        _ = await pipeline.ReadAsync<int>(PlcReplacementPipelineOperations.ReadCurrentValue, "DB1.1");

        var snapshot = monitoringLogPipeline.Snapshot();

        Assert.Single(snapshot);
        Assert.Equal(WearPartMonitoringLogCategory.Plc, snapshot[0].Category);
        Assert.Equal(WearPartMonitoringLogLevel.Information, snapshot[0].Level);
        Assert.Equal(PlcMonitoringPipelineOperations.ReadCurrentValue, snapshot[0].OperationName);
        Assert.Equal("DB1.0", snapshot[0].Address);
    }

    [Fact]
    public async Task ReadAsync_WhenMonitoringOperationFails_ShouldPublishUiErrorLogEntry()
    {
        var plcService = new PipelineTestPlcService();
        var logger = new TestLogger<PlcOperationPipeline>();
        var monitoringLogPipeline = new WearPartMonitoringLogPipeline();
        var pipeline = new PlcOperationPipeline(plcService, logger, monitoringLogPipeline: monitoringLogPipeline);

        plcService.OnRead = _ => throw new InvalidOperationException("read failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.ReadAsync<int>(PlcMonitoringPipelineOperations.ReadCurrentValue, "DB1.0"));

        var entry = Assert.Single(monitoringLogPipeline.Snapshot());
        Assert.Equal(WearPartMonitoringLogLevel.Error, entry.Level);
        Assert.Equal(WearPartMonitoringLogCategory.Plc, entry.Category);
        Assert.Equal("DB1.0", entry.Address);
        Assert.Contains("read failed", entry.Message, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", entry.Details, StringComparison.Ordinal);
    }

    private sealed class PipelineTestPlcService : IPlcService
    {
        public bool IsConnected => true;

        public Func<PlcConnectionOptions, bool, Task>? OnConnectAsync { get; set; }

        public Func<string, object>? OnRead { get; set; }

        public Task ConnectAsync(PlcConnectionOptions options, bool forceReconnect = false, CancellationToken cancellationToken = default)
        {
            return OnConnectAsync?.Invoke(options, forceReconnect) ?? Task.CompletedTask;
        }

        public void Disconnect()
        {
        }

        public TValue Read<TValue>(string address, int retryCount = 1)
        {
            if (OnRead is null)
            {
                throw new NotSupportedException();
            }

            var value = OnRead(address);
            return (TValue)Convert.ChangeType(value, typeof(TValue), System.Globalization.CultureInfo.InvariantCulture);
        }

        public void Write<TValue>(string address, TValue value, int retryCount = 1)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public AppSettings Current { get; set; } = new();

        public event EventHandler<AppSettings>? SettingsSaved;

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Current);
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            SettingsSaved?.Invoke(this, settings);
            return ValueTask.CompletedTask;
        }
    }
}