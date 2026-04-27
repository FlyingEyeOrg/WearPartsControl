using System.Globalization;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartAlertPopupService : IWearPartAlertPopupService
{
    private readonly ISaveInfoStore _saveInfoStore;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IWearPartAlertPresenter _presenter;

    public WearPartAlertPopupService(ISaveInfoStore saveInfoStore, IUiDispatcher uiDispatcher, IWearPartAlertPresenter presenter)
    {
        _saveInfoStore = saveInfoStore;
        _uiDispatcher = uiDispatcher;
        _presenter = presenter;
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

        await _uiDispatcher.RunAsync(() =>
        {
            _presenter.Show(title, markdown);
        }).ConfigureAwait(false);

        state.LastShownLocalDate = localDate;
        await _saveInfoStore.WriteAsync(state, cancellationToken).ConfigureAwait(false);
    }
}