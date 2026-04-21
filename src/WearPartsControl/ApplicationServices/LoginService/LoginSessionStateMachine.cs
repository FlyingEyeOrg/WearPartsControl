using System.Threading;
using WearPartsControl.ApplicationServices.AppSettings;
using AppSettingsModel = WearPartsControl.ApplicationServices.AppSettings.AppSettings;

namespace WearPartsControl.ApplicationServices.LoginService;

public sealed class LoginSessionStateMachine : ILoginSessionStateMachine
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ILoginService _loginService;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly object _stateLock = new();
    private int _autoLogoutCountdownSeconds = 360;
    private int _remainingAutoLogoutSeconds;
    private CancellationTokenSource? _autoLogoutCancellationTokenSource;

    public LoginSessionStateMachine(
        ICurrentUserAccessor currentUserAccessor,
        ILoginService loginService,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _currentUserAccessor = currentUserAccessor;
        _loginService = loginService;
        _delayAsync = delayAsync ?? Task.Delay;
        Current = currentUserAccessor.CurrentUser is { } currentUser
            ? new LoginSessionState(currentUser, _autoLogoutCountdownSeconds)
            : LoginSessionState.LoggedOut;

        _currentUserAccessor.CurrentUserChanged += OnCurrentUserChanged;
    }

    public event EventHandler? StateChanged;

    public LoginSessionState Current { get; private set; }

    public void UpdateSettings(AppSettingsModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _autoLogoutCountdownSeconds = settings.AutoLogoutCountdownSeconds <= 0
            ? 360
            : settings.AutoLogoutCountdownSeconds;

        if (_currentUserAccessor.CurrentUser is { } currentUser)
        {
            StartAutoLogoutCountdown(currentUser);
            return;
        }

        Publish(LoginSessionState.LoggedOut);
    }

    private void OnCurrentUserChanged(object? sender, EventArgs e)
    {
        if (_currentUserAccessor.CurrentUser is { } currentUser)
        {
            StartAutoLogoutCountdown(currentUser);
            return;
        }

        StopAutoLogoutCountdown();
        Publish(LoginSessionState.LoggedOut);
    }

    private void StartAutoLogoutCountdown(MhrUser currentUser)
    {
        StopAutoLogoutCountdown();

        _remainingAutoLogoutSeconds = _autoLogoutCountdownSeconds;
        Publish(new LoginSessionState(currentUser, _remainingAutoLogoutSeconds));

        var cts = new CancellationTokenSource();
        lock (_stateLock)
        {
            _autoLogoutCancellationTokenSource = cts;
        }

        _ = RunAutoLogoutCountdownAsync(currentUser, cts.Token);
    }

    private async Task RunAutoLogoutCountdownAsync(MhrUser currentUser, CancellationToken cancellationToken)
    {
        try
        {
            while (_remainingAutoLogoutSeconds > 0)
            {
                await _delayAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                _remainingAutoLogoutSeconds--;
                if (_currentUserAccessor.CurrentUser is not null)
                {
                    Publish(new LoginSessionState(currentUser, _remainingAutoLogoutSeconds));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            await _loginService.LogoutAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void StopAutoLogoutCountdown()
    {
        CancellationTokenSource? cts;
        lock (_stateLock)
        {
            cts = _autoLogoutCancellationTokenSource;
            _autoLogoutCancellationTokenSource = null;
        }

        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _remainingAutoLogoutSeconds = 0;
    }

    private void Publish(LoginSessionState state)
    {
        Current = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}