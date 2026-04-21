using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices;

public abstract class ApplicationService
{
    protected ApplicationService(ICurrentUserAccessor currentUserAccessor)
    {
        CurrentUserAccessor = currentUserAccessor;
    }

    protected ICurrentUserAccessor CurrentUserAccessor { get; }

    protected MhrUser? CurrentUser => CurrentUserAccessor.CurrentUser;

    protected string? CurrentUserId => CurrentUserAccessor.UserId;

    protected MhrUser GetRequiredCurrentUser()
    {
        return CurrentUser ?? throw new AuthorizationException(LocalizedText.Get("Services.Authorization.CurrentUserMissing"));
    }

    protected string GetRequiredCurrentUserId()
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new AuthorizationException(LocalizedText.Get("Services.Authorization.CurrentUserMissing"));
        }

        return userId;
    }

    protected MhrUser EnsureAccessLevel(int minimumAccessLevel)
    {
        if (minimumAccessLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAccessLevel));
        }

        var user = GetRequiredCurrentUser();
        if (user.AccessLevel < minimumAccessLevel)
        {
            throw new AuthorizationException(LocalizedText.Format("Services.Authorization.AccessLevelDenied", minimumAccessLevel), code: "Authorization:AccessLevelDenied");
        }

        return user;
    }
}