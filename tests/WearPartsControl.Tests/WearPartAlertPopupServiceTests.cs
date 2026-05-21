using System.IO;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.SaveInfoService;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class WearPartAlertPopupServiceTests : IDisposable
{
    private readonly string _settingsDirectory = Path.Combine(Path.GetTempPath(), $"wear-part-alert-popup-{Guid.NewGuid():N}");

    [Fact]
    public async Task ShowIfNeededAsync_WhenSamePartSameDayRepeated_ShouldOnlyShowOnce()
    {
        var store = new TypeJsonSaveInfoStore(_settingsDirectory);
        var presenter = new FakeWearPartAlertPresenter();
        var service = new WearPartAlertPopupService(store, new ImmediateUiDispatcher(), presenter);
        var partId = Guid.NewGuid();

        await service.ShowIfNeededAsync(partId, "预警通知", "# 通知\n\n- **易损件**：刀具A", new DateTime(2026, 4, 27, 8, 0, 0, DateTimeKind.Local));
        await service.ShowIfNeededAsync(partId, "停机通知", "# 通知\n\n- **易损件**：刀具A", new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Local));

        Assert.Equal(1, presenter.ShowCount);

        var state = await store.ReadAsync<WearPartAlertPopupSaveInfo>();
        Assert.True(state.PartDailyStates.TryGetValue(partId, out var lastDate));
        Assert.Equal("2026-04-27", lastDate);
    }

    [Fact]
    public async Task ShowIfNeededAsync_WhenNextDayArrives_ShouldShowAgain()
    {
        var store = new TypeJsonSaveInfoStore(_settingsDirectory);
        var presenter = new FakeWearPartAlertPresenter();
        var service = new WearPartAlertPopupService(store, new ImmediateUiDispatcher(), presenter);
        var partId = Guid.NewGuid();

        await service.ShowIfNeededAsync(partId, "预警通知", "# 通知\n\n- **易损件**：刀具A", new DateTime(2026, 4, 27, 8, 0, 0, DateTimeKind.Local));
        await service.ShowIfNeededAsync(partId, "预警通知", "# 通知\n\n- **易损件**：刀具A", new DateTime(2026, 4, 28, 8, 0, 0, DateTimeKind.Local));

        Assert.Equal(2, presenter.ShowCount);
    }

    [Fact]
    public async Task ShowIfNeededAsync_WhenDifferentPartsSameDay_ShouldBothShow()
    {
        var store = new TypeJsonSaveInfoStore(_settingsDirectory);
        var presenter = new FakeWearPartAlertPresenter();
        var service = new WearPartAlertPopupService(store, new ImmediateUiDispatcher(), presenter);
        var partA = Guid.NewGuid();
        var partB = Guid.NewGuid();
        var date = new DateTime(2026, 4, 27, 8, 0, 0, DateTimeKind.Local);

        await service.ShowIfNeededAsync(partA, "预警通知A", "# 通知\n\n- **易损件**：刀具A", date);
        await service.ShowIfNeededAsync(partB, "预警通知B", "# 通知\n\n- **易损件**：刀具B", date);

        Assert.Equal(2, presenter.ShowCount);

        var state = await store.ReadAsync<WearPartAlertPopupSaveInfo>();
        Assert.True(state.PartDailyStates.TryGetValue(partA, out _));
        Assert.True(state.PartDailyStates.TryGetValue(partB, out _));
    }

    [Fact]
    public async Task ShowIfNeededAsync_WhenUiDispatcherDoesNotComplete_ShouldReturnAfterQueueingPopup()
    {
        var store = new TypeJsonSaveInfoStore(_settingsDirectory);
        var presenter = new FakeWearPartAlertPresenter();
        var dispatcher = new NonCompletingUiDispatcher();
        var service = new WearPartAlertPopupService(store, dispatcher, presenter);
        var partId = Guid.NewGuid();

        var showTask = service.ShowIfNeededAsync(partId, "预警通知", "# 通知\n\n- **易损件**：刀具A", new DateTime(2026, 4, 27, 8, 0, 0, DateTimeKind.Local)).AsTask();
        var completedTask = await Task.WhenAny(showTask, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.Same(showTask, completedTask);
        Assert.True(showTask.IsCompletedSuccessfully);
        Assert.Equal(1, dispatcher.RunAsyncCallCount);
        Assert.Equal(1, presenter.ShowCount);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_settingsDirectory))
            {
                Directory.Delete(_settingsDirectory, true);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed class FakeWearPartAlertPresenter : IWearPartAlertPresenter
    {
        public int ShowCount { get; private set; }

        public void Show(string title, string markdown)
        {
            ShowCount++;
        }
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public void Run(Action action)
        {
            action();
        }

        public Task RunAsync(Action action, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
        {
            action();
            return Task.CompletedTask;
        }

        public Task RenderAsync()
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NonCompletingUiDispatcher : IUiDispatcher
    {
        private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RunAsyncCallCount { get; private set; }

        public void Run(Action action)
        {
            action();
        }

        public Task RunAsync(Action action, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
        {
            RunAsyncCallCount++;
            action();
            return _completionSource.Task;
        }

        public Task RenderAsync()
        {
            return Task.CompletedTask;
        }
    }
}