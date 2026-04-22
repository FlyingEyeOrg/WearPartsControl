using Microsoft.Extensions.Logging;
using WearPartsControl.ApplicationServices.PlcService;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class PlcOperationPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCalledConcurrently_ShouldSerializeOperations()
    {
        var plcService = new PipelineTestPlcService();
        var logger = new TestLogger<PlcOperationPipeline>();
        var pipeline = new PlcOperationPipeline(plcService, logger);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = false;
        var executionOrder = new List<string>();

        var firstTask = pipeline.ExecuteAsync("Test/First", async _ =>
        {
            executionOrder.Add("first-start");
            firstEntered.SetResult();
            await releaseFirst.Task;
            executionOrder.Add("first-end");
        });

        await firstEntered.Task;

        var secondTask = pipeline.ExecuteAsync("Test/Second", _ =>
        {
            secondStarted = true;
            executionOrder.Add("second");
        });

        await Task.Delay(100);
        Assert.False(secondStarted);

        releaseFirst.SetResult();
        await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(new[] { "first-start", "first-end", "second" }, executionOrder);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationIsSlow_ShouldWriteWarningLog()
    {
        var plcService = new PipelineTestPlcService();
        var logger = new TestLogger<PlcOperationPipeline>();
        var pipeline = new PlcOperationPipeline(plcService, logger);

        await pipeline.ExecuteAsync("Test/SlowOperation", async _ =>
        {
            await Task.Delay(550);
        });

        Assert.Contains(logger.Entries, entry =>
            entry.LogLevel == LogLevel.Warning
            && entry.Message.Contains("Test/SlowOperation", StringComparison.Ordinal)
            && entry.Message.Contains("completed in", StringComparison.Ordinal));
    }

    private sealed class PipelineTestPlcService : IPlcOperationContext
    {
        public bool IsConnected => true;

        public Task ConnectAsync(PlcConnectionOptions options, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
        }

        public TValue Read<TValue>(string address, int retryCount = 1)
        {
            throw new NotSupportedException();
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
}