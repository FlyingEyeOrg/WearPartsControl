using System.Threading.Tasks;

namespace WearPartsControl.ApplicationServices.LoginService;

public interface ILoginService
{
    Task<UserModel?> LoginAsync(string authId, string factory, string resourceId, bool isIdCard, CancellationToken cancellationToken = default);
}