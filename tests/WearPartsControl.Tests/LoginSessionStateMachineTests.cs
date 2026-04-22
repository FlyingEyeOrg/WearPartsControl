using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.LoginService;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class LoginSessionStateMachineTests
{
    [Fact]
    public async Task EnterInteractionScope_WhenLoggedIn_ShouldPauseCountdownAndRestartFromFullDurationOnDispose()
    {
        var accessor = new CurrentUserAccessor();
        var loginService = new StubLoginService(accessor);
        var delaySignals = new List<TaskCompletionSource<bool>>();
        var stateMachine = new LoginSessionStateMachine(
            accessor,
            loginService,
            (delay, cancellationToken) =>
            {
                var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                delaySignals.Add(signal);
                cancellationToken.Register(() => signal.TrySetCanceled(cancellationToken));
                return signal.Task;
            });

        stateMachine.UpdateSettings(new AppSettings { AutoLogoutCountdownSeconds = 3 });
        accessor.SetCurrentUser(new MhrUser { CardId = "CARD-01", WorkId = "WORK-01", AccessLevel = 2 });

        Assert.Equal(3, stateMachine.Current.RemainingAutoLogoutSeconds);
        Assert.Single(delaySignals);

        using (stateMachine.EnterInteractionScope())
        {
            await WaitUntilAsync(() => delaySignals[0].Task.IsCanceled);
            Assert.Equal(3, stateMachine.Current.RemainingAutoLogoutSeconds);
            Assert.Equal(0, loginService.LogoutCount);
        }

        await WaitUntilAsync(() => delaySignals.Count == 2);
        Assert.Equal(3, stateMachine.Current.RemainingAutoLogoutSeconds);

        delaySignals[1].SetResult(true);
        await WaitUntilAsync(() => stateMachine.Current.RemainingAutoLogoutSeconds == 2);
    }

    [Fact]
    public async Task EnterInteractionScope_WhenNested_ShouldResumeOnlyAfterOuterScopeDisposed()
    {
        var accessor = new CurrentUserAccessor();
        var loginService = new StubLoginService(accessor);
        var delaySignals = new List<TaskCompletionSource<bool>>();
        var stateMachine = new LoginSessionStateMachine(
            accessor,
            loginService,
            (delay, cancellationToken) =>
            {
                var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                delaySignals.Add(signal);
                cancellationToken.Register(() => signal.TrySetCanceled(cancellationToken));
                return signal.Task;
            });

        stateMachine.UpdateSettings(new AppSettings { AutoLogoutCountdownSeconds = 3 });
        accessor.SetCurrentUser(new MhrUser { CardId = "CARD-01", WorkId = "WORK-01", AccessLevel = 2 });

        using var outer = stateMachine.EnterInteractionScope();
        using var inner = stateMachine.EnterInteractionScope();
        await WaitUntilAsync(() => delaySignals[0].Task.IsCanceled);

        inner.Dispose();
        await Task.Delay(20);
        Assert.Single(delaySignals);

        outer.Dispose();
        await WaitUntilAsync(() => delaySignals.Count == 2);
    }

    [Fact]
    public async Task ResetAutoLogoutCountdown_WhenLoggedIn_ShouldRestartFromFullDuration()
    {
        var accessor = new CurrentUserAccessor();
        var loginService = new StubLoginService(accessor);
        var delaySignals = new List<TaskCompletionSource<bool>>();
        var stateMachine = new LoginSessionStateMachine(
            accessor,
            loginService,
            (delay, cancellationToken) =>
            {
                var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                delaySignals.Add(signal);
                cancellationToken.Register(() => signal.TrySetCanceled(cancellationToken));
                return signal.Task;
            });

        stateMachine.UpdateSettings(new AppSettings { AutoLogoutCountdownSeconds = 3 });
        accessor.SetCurrentUser(new MhrUser { CardId = "CARD-01", WorkId = "WORK-01", AccessLevel = 2 });

        delaySignals[0].SetResult(true);
        await WaitUntilAsync(() => stateMachine.Current.RemainingAutoLogoutSeconds == 2);

        var signalCountBeforeReset = delaySignals.Count;
        stateMachine.ResetAutoLogoutCountdown();

        Assert.Equal(3, stateMachine.Current.RemainingAutoLogoutSeconds);
        await WaitUntilAsync(() => delaySignals.Count > signalCountBeforeReset);

        delaySignals[^1].SetResult(true);
        await WaitUntilAsync(() => stateMachine.Current.RemainingAutoLogoutSeconds == 2);
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

    private sealed class StubLoginService : ILoginService
    {
        private readonly CurrentUserAccessor _currentUserAccessor;

        public StubLoginService(CurrentUserAccessor currentUserAccessor)
        {
            _currentUserAccessor = currentUserAccessor;
        }

        public int LogoutCount { get; private set; }

        public Task<MhrUser?> LoginAsync(string authId, string factory, string resourceId, bool isIdCard, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public MhrUser? GetCurrentUser() => _currentUserAccessor.CurrentUser;

        public ValueTask LogoutAsync(CancellationToken cancellationToken = default)
        {
            LogoutCount++;
            _currentUserAccessor.Clear();
            return ValueTask.CompletedTask;
        }
    }
}