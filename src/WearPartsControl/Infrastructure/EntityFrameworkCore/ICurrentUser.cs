namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public interface ICurrentUser
{
    string? UserId { get; }
}
