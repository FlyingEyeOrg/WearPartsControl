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
    public async Task ShowIfNeededAsync_WhenSameDayRepeated_ShouldOnlyShowOnce()
    {
        var store = new TypeJsonSaveInfoStore(_settingsDirectory);
        var presenter = new FakeWearPartAlertPresenter();
        var service = new WearPartAlertPopupService(store, new ImmediateUiDispatcher(), presenter);

        await service.ShowIfNeededAsync("预警通知", "# 通知\n\n- **易损件**：刀具A", new DateTime(2026, 4, 27, 8, 0, 0, DateTimeKind.Local));
        await service.ShowIfNeededAsync("停机通知", "# 通知\n\n- **易损件**：刀具A", new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Local));

        Assert.Equal(1, presenter.ShowCount);

        var state = await store.ReadAsync<WearPartAlertPopupSaveInfo>();
        Assert.Equal("2026-04-27", state.LastShownLocalDate);
    }

    [Fact]
    public async Task ShowIfNeededAsync_WhenNextDayArrives_ShouldShowAgain()
    {
        var store = new TypeJsonSaveInfoStore(_settingsDirectory);
        var presenter = new FakeWearPartAlertPresenter();
        var service = new WearPartAlertPopupService(store, new ImmediateUiDispatcher(), presenter);

        await service.ShowIfNeededAsync("预警通知", "# 通知\n\n- **易损件**：刀具A", new DateTime(2026, 4, 27, 8, 0, 0, DateTimeKind.Local));
        await service.ShowIfNeededAsync("预警通知", "# 通知\n\n- **易损件**：刀具A", new DateTime(2026, 4, 28, 8, 0, 0, DateTimeKind.Local));

        Assert.Equal(2, presenter.ShowCount);
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
}