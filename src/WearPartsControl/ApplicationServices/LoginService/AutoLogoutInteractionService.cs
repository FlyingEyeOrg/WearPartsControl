namespace WearPartsControl.ApplicationServices.LoginService;

public sealed class AutoLogoutInteractionService : IAutoLogoutInteractionService
{
    private readonly ILoginSessionStateMachine _loginSessionStateMachine;

    public AutoLogoutInteractionService(ILoginSessionStateMachine loginSessionStateMachine)
    {
        _loginSessionStateMachine = loginSessionStateMachine;
    }

    public void NotifyActivity()
    {
        _loginSessionStateMachine.ResetAutoLogoutCountdown();
    }

    public TResult RunModal<TResult>(Func<TResult> interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        using var _ = _loginSessionStateMachine.EnterInteractionScope();
        return interaction();
    }
}