using System.Diagnostics;
using WearPartsControl.ApplicationServices.MonitoringLogs;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class WearPartMonitoringLogPipelineTests
{
    [Fact]
    public async Task Publish_WhenCalledConcurrently_ShouldRetainBoundedSnapshotAndRaiseEvents()
    {
        using var pipeline = new WearPartMonitoringLogPipeline(capacity: 25);
        var eventCount = 0;
        pipeline.EntriesAdded += (_, e) => Interlocked.Add(ref eventCount, e.Entries.Count);

        var tasks = Enumerable.Range(0, 100)
            .Select(index => Task.Run(() => pipeline.Publish(
                WearPartMonitoringLogLevel.Information,
                WearPartMonitoringLogCategory.Plc,
                $"message-{index}",
                operationName: "Monitor/ReadCurrentValue",
                address: $"DB1.{index}")))
            .ToArray();

        await Task.WhenAll(tasks);
        await WaitUntilAsync(() => Volatile.Read(ref eventCount) == 100);

        var snapshot = pipeline.Snapshot();

        Assert.Equal(100, eventCount);
        Assert.Equal(25, snapshot.Count);
        Assert.Equal(25, pipeline.RetainedCount);
        Assert.Equal(snapshot.Count, snapshot.Select(entry => entry.Sequence).Distinct().Count());
        Assert.All(snapshot, entry => Assert.Equal(WearPartMonitoringLogCategory.Plc, entry.Category));
    }

    [Fact]
    public void Query_ShouldReturnOnlyRequestedNewestPageAndMatchingCount()
    {
        using var pipeline = new WearPartMonitoringLogPipeline(capacity: 10);
        for (var index = 0; index < 30; index++)
        {
            pipeline.Publish(
                WearPartMonitoringLogLevel.Information,
                WearPartMonitoringLogCategory.Service,
                $"message-{index}");
        }

        var firstPage = pipeline.Query(new WearPartMonitoringLogQuery(null, null, null, 0, 4));
        var secondPage = pipeline.Query(new WearPartMonitoringLogQuery(null, null, null, 4, 4));

        Assert.Equal(10, firstPage.RetainedCount);
        Assert.Equal(10, firstPage.TotalCount);
        Assert.Equal(new[] { "message-26", "message-27", "message-28", "message-29" }, firstPage.Entries.Select(entry => entry.Message));
        Assert.Equal(new[] { "message-22", "message-23", "message-24", "message-25" }, secondPage.Entries.Select(entry => entry.Message));
    }

    [Fact]
    public async Task Publish_WhenSubscriberBlocks_ShouldReturnWithoutWaitingForUiDispatch()
    {
        using var pipeline = new WearPartMonitoringLogPipeline(capacity: 25);
        using var releaseHandler = new ManualResetEventSlim(false);
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pipeline.EntriesAdded += (_, _) =>
        {
            handlerStarted.TrySetResult();
            releaseHandler.Wait(TimeSpan.FromSeconds(5));
        };

        var stopwatch = Stopwatch.StartNew();
        pipeline.Publish(WearPartMonitoringLogLevel.Information, WearPartMonitoringLogCategory.Service, "submitted");
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 250);

        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        releaseHandler.Set();
    }

    [Fact]
    public async Task Clear_ShouldRemoveSnapshotAndRaiseClearedEvent()
    {
        using var pipeline = new WearPartMonitoringLogPipeline();
        var clearedCount = 0;
        pipeline.Cleared += (_, _) => clearedCount++;

        pipeline.Publish(WearPartMonitoringLogLevel.Warning, WearPartMonitoringLogCategory.Service, "warning");
        pipeline.Clear();

        await WaitUntilAsync(() => Volatile.Read(ref clearedCount) == 1);

        Assert.Empty(pipeline.Snapshot());
        Assert.Equal(0, pipeline.RetainedCount);
        Assert.Equal(1, clearedCount);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(predicate());
    }
}