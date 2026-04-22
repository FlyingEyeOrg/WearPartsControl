namespace WearPartsControl.ApplicationServices.LoginService;

public interface IAutoLogoutInteractionService
{
    void NotifyActivity();

    TResult RunModal<TResult>(Func<TResult> interaction);
}