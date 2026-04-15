using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.ApplicationServices.ComNotification;

public interface IComNotificationService
{
    ValueTask NotifyGroupAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default);

    ValueTask NotifyWorkAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default);
}
