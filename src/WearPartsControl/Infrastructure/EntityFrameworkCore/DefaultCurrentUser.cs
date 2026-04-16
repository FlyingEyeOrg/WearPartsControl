namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public sealed class DefaultCurrentUser : ICurrentUser
{
    public string UserId => "system";
}
