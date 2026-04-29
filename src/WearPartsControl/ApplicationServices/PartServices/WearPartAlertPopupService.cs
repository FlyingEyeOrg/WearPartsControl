using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartAlertPopupService : IWearPartAlertPopupService
{
    private readonly ISaveInfoStore _saveInfoStore;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IWearPartAlertPresenter _presenter;
    private readonly ILogger<WearPartAlertPopupService> _logger;

    public WearPartAlertPopupService(
        ISaveInfoStore saveInfoStore,
        IUiDispatcher uiDispatcher,
        IWearPartAlertPresenter presenter,
        ILogger<WearPartAlertPopupService>? logger = null)
    {
        _saveInfoStore = saveInfoStore;
        _uiDispatcher = uiDispatcher;
        _presenter = presenter;
        _logger = logger ?? NullLogger<WearPartAlertPopupService>.Instance;
    }

    public async ValueTask ShowIfNeededAsync(string title, string markdown, DateTime occurredAt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(markdown))
        {
            return;
        }

        var state = await _saveInfoStore.ReadAsync<WearPartAlertPopupSaveInfo>(cancellationToken).ConfigureAwait(false);
        var localDate = occurredAt.ToLocalTime().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (string.Equals(state.LastShownLocalDate, localDate, StringComparison.Ordinal))
        {
            return;
        }

        QueuePopup(title, markdown);

        state.LastShownLocalDate = localDate;
        await _saveInfoStore.WriteAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private void QueuePopup(string title, string markdown)
    {
        try
        {
            var popupTask = _uiDispatcher.RunAsync(() => _presenter.Show(title, markdown));
            if (!popupTask.IsCompletedSuccessfully)
            {
                _ = LogPopupFailureAsync(popupTask);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to queue wear-part alert popup.");
        }
    }

    private async Task LogPopupFailureAsync(Task popupTask)
    {
        try
        {
            await popupTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show wear-part alert popup.");
        }
    }
}
