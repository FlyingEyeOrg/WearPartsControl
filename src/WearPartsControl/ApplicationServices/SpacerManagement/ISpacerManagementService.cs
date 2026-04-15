using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.ApplicationServices.SpacerManagement;

public interface ISpacerManagementService
{
    SpacerInfo ParseCode(string code, string site, string resourceId, string cardId);

    ValueTask VerifyAsync(SpacerInfo info, CancellationToken cancellationToken = default);
}
