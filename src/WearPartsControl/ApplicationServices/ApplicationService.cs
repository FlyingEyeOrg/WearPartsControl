using WearPartsControl.ApplicationServices.LoginService;
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
        return CurrentUser ?? throw new AuthorizationException("当前没有已登录用户。");
    }

    protected string GetRequiredCurrentUserId()
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new AuthorizationException("当前没有已登录用户。");
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
            throw new AuthorizationException($"当前用户权限不足，要求权限等级不低于 {minimumAccessLevel}。", code: "Authorization:AccessLevelDenied");
        }

        return user;
    }
}