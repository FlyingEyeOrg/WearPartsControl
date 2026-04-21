namespace WearPartsControl.ApplicationServices.LoginService;

public sealed record LoginSessionState(MhrUser? CurrentUser, int RemainingAutoLogoutSeconds)
{
    public static LoginSessionState LoggedOut { get; } = new(null, 0);

    public bool IsLoggedIn => CurrentUser is not null;
}