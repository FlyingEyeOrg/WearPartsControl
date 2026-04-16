using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.Infrastructure.EntityFrameworkCore;

namespace WearPartsControl.ApplicationServices;

public interface ICurrentUserAccessor : ICurrentUser
{
    MhrUser? CurrentUser { get; }

    void SetCurrentUser(MhrUser? user);

    void Clear();
}