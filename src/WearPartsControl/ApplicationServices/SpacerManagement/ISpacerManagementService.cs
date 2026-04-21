using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.ApplicationServices.SpacerManagement;

public interface ISpacerManagementService
{
    ValueTask<SpacerInfo> ParseCodeAsync(string code, string site, string resourceId, string cardId, CancellationToken cancellationToken = default);

    ValueTask VerifyAsync(SpacerInfo info, CancellationToken cancellationToken = default);
}
