using System.Windows.Threading;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.MonitoringLogs;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(LocalizationSensitiveTestCollection.Name)]
public sealed class WearPartMonitoringLogViewModelTests : IDisposable
{
    private readonly TestCultureScope _cultureScope = new("en-US");

    [Fact]
    public async Task EntriesAdded_FromWorkerThread_ShouldMarshalToUiDispatcher()
    {
        using var pipeline = new WearPartMonitoringLogPipeline();
        var dispatcher = new RecordingUiDispatcher();
        using var viewModel = new WearPartMonitoringLogViewModel(pipeline, dispatcher);

        await Task.Run(() => pipeline.Publish(
            WearPartMonitoringLogLevel.Information,
            WearPartMonitoringLogCategory.Plc,
            "read succeeded",
            operationName: "Monitor/ReadCurrentValue",
            resourceNumber: "RES-01",
            address: "DB1.0"));

        await WaitUntilAsync(() => viewModel.Entries.Count == 1);

        Assert.True(dispatcher.RunAsyncCallCount > 0);
        Assert.Equal("DB1.0", viewModel.Entries[0].Address);
        Assert.Contains("1", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PauseResume_ShouldBufferVisibleUpdatesUntilResumed()
    {
        using var pipeline = new WearPartMonitoringLogPipeline();
        var dispatcher = new RecordingUiDispatcher();
        using var viewModel = new WearPartMonitoringLogViewModel(pipeline, dispatcher);

        viewModel.PauseResumeCommand.Execute(null);

        await Task.Run(() => pipeline.Publish(
            WearPartMonitoringLogLevel.Warning,
            WearPartMonitoringLogCategory.Service,
            "paused entry"));

        await WaitUntilAsync(() => viewModel.StatusMessage.Contains("1/", StringComparison.Ordinal));
        Assert.Empty(viewModel.Entries);

        viewModel.PauseResumeCommand.Execute(null);

        Assert.Single(viewModel.Entries);
        Assert.Equal("paused entry", viewModel.Entries[0].Message);
    }

    [Fact]
    public async Task KeywordFilter_ShouldMatchOperationResourceAddressAndMessage()
    {
        using var pipeline = new WearPartMonitoringLogPipeline();
        using var viewModel = new WearPartMonitoringLogViewModel(pipeline, new RecordingUiDispatcher());

        pipeline.Publish(WearPartMonitoringLogLevel.Information, WearPartMonitoringLogCategory.Plc, "current lifetime", operationName: "Monitor/ReadCurrentValue", resourceNumber: "RES-01", address: "DB1.0");
        pipeline.Publish(WearPartMonitoringLogLevel.Information, WearPartMonitoringLogCategory.Com, "notification sent", operationName: "group message", resourceNumber: "RES-02");

        await WaitUntilAsync(() => viewModel.Entries.Count == 2);

        viewModel.Keyword = "DB1.0";

        Assert.Single(viewModel.Entries);
        Assert.Equal("RES-01", viewModel.Entries[0].ResourceNumber);
    }

    [Fact]
    public async Task Entries_ShouldLoadOnlyCurrentPageAndFetchOlderPageOnDemand()
    {
        using var pipeline = new WearPartMonitoringLogPipeline();
        using var viewModel = new WearPartMonitoringLogViewModel(pipeline, new RecordingUiDispatcher());

        for (var index = 0; index < 250; index++)
        {
            pipeline.Publish(WearPartMonitoringLogLevel.Information, WearPartMonitoringLogCategory.Service, $"message-{index}");
        }

        await WaitUntilAsync(() => viewModel.Entries.Count == viewModel.PageSize);

        Assert.Equal(200, viewModel.Entries.Count);
        Assert.Equal(1, viewModel.CurrentPageNumber);
        Assert.Equal(2, viewModel.TotalPageCount);
        Assert.Equal("message-50", viewModel.Entries[0].Message);

        Assert.True(viewModel.OlderPageCommand.CanExecute(null));
        viewModel.OlderPageCommand.Execute(null);

        Assert.Equal(50, viewModel.Entries.Count);
        Assert.Equal(2, viewModel.CurrentPageNumber);
        Assert.Equal("message-0", viewModel.Entries[0].Message);
    }

    public void Dispose()
    {
        _cultureScope.Dispose();
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

    private sealed class RecordingUiDispatcher : IUiDispatcher
    {
        public int RunAsyncCallCount { get; private set; }

        public void Run(Action action) => action();

        public Task RunAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            RunAsyncCallCount++;
            action();
            return Task.CompletedTask;
        }

        public Task RenderAsync() => Task.CompletedTask;
    }
}