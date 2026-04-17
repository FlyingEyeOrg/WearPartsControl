using WearPartsControl.ApplicationServices;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class UiBusyServiceTests
{
    [Fact]
    public void Constructor_WithoutOverride_ShouldUseCurrentDefaultMinimumDuration()
    {
        var service = new UiBusyService();

        Assert.Equal(TimeSpan.FromMilliseconds(500), service.MinimumBusyDuration);
    }

    [Fact]
    public void Enter_ShouldSetBusyImmediately()
    {
        var service = new UiBusyService(TimeSpan.FromMilliseconds(50));

        using var scope = service.Enter();

        Assert.True(service.IsBusy);
    }

    [Fact]
    public async Task Dispose_ShouldKeepBusyUntilMinimumDurationElapsed()
    {
        var delayStarted = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDelayCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new UiBusyService(
            TimeSpan.FromMilliseconds(50),
            async (delay, cancellationToken) =>
            {
                delayStarted.TrySetResult(delay);
                await allowDelayCompletion.Task;
            });

        var scope = service.Enter();
        scope.Dispose();

        var requestedDelay = await delayStarted.Task;

        Assert.True(service.IsBusy);
        Assert.True(requestedDelay > TimeSpan.Zero);

        allowDelayCompletion.SetResult();
        await WaitUntilAsync(() => !service.IsBusy);

        Assert.False(service.IsBusy);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var index = 0; index < 20; index++)
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