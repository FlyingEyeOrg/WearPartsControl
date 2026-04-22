using WearPartsControl.ApplicationServices.LoginService;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class AutoLogoutInteractionServiceTests
{
    [Fact]
    public void NotifyActivity_ShouldResetAutoLogoutCountdown()
    {
        var stateMachine = new StubLoginSessionStateMachine();
        var service = new AutoLogoutInteractionService(stateMachine);

        service.NotifyActivity();

        Assert.Equal(1, stateMachine.ResetAutoLogoutCountdownCallCount);
    }

    [Fact]
    public void RunModal_ShouldWrapInteractionScope()
    {
        var stateMachine = new StubLoginSessionStateMachine();
        var service = new AutoLogoutInteractionService(stateMachine);

        var result = service.RunModal(() =>
        {
            Assert.Equal(1, stateMachine.ActiveInteractionScopeCount);
            return true;
        });

        Assert.True(result);
        Assert.Equal(1, stateMachine.EnterInteractionScopeCallCount);
        Assert.Equal(0, stateMachine.ActiveInteractionScopeCount);
    }

    private sealed class StubLoginSessionStateMachine : ILoginSessionStateMachine
    {
        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public LoginSessionState Current { get; } = LoginSessionState.LoggedOut;

        public int EnterInteractionScopeCallCount { get; private set; }

        public int ResetAutoLogoutCountdownCallCount { get; private set; }

        public int ActiveInteractionScopeCount { get; private set; }

        public void UpdateSettings(ApplicationServices.AppSettings.AppSettings settings)
        {
        }

        public IDisposable EnterInteractionScope()
        {
            EnterInteractionScopeCallCount++;
            ActiveInteractionScopeCount++;
            return new Scope(this);
        }

        public void ResetAutoLogoutCountdown()
        {
            ResetAutoLogoutCountdownCallCount++;
        }

        private sealed class Scope : IDisposable
        {
            private StubLoginSessionStateMachine? _owner;

            public Scope(StubLoginSessionStateMachine owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner is null)
                {
                    return;
                }

                _owner.ActiveInteractionScopeCount--;
                _owner = null;
            }
        }
    }
}