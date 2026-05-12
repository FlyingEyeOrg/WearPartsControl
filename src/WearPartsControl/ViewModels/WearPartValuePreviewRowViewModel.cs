using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class WearPartValuePreviewRowViewModel : ObservableObject
{
    private readonly WearPartMonitorStatus _status;

    public WearPartValuePreviewRowViewModel(WearPartValuePreviewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        PartName = item.PartName;
        WearPartTypeName = item.WearPartTypeName;
        LifetimeType = item.LifetimeType;
        CurrentValue = item.CurrentValue;
        WarningValue = item.WarningValue;
        ShutdownValue = item.ShutdownValue;
        _status = item.Status;
    }

    public string PartName { get; }

    public string WearPartTypeName { get; }

    public string LifetimeType { get; }

    public double CurrentValue { get; }

    public double WarningValue { get; }

    public double ShutdownValue { get; }

    public string StatusText => _status switch
    {
        WearPartMonitorStatus.Warning => LocalizedText.Get("WearPartValuePreviewControl.StatusWarning"),
        WearPartMonitorStatus.Shutdown => LocalizedText.Get("WearPartValuePreviewControl.StatusShutdown"),
        _ => LocalizedText.Get("WearPartValuePreviewControl.StatusNormal")
    };

    public Brush StatusBackground => _status switch
    {
        WearPartMonitorStatus.Warning => Brushes.DarkGoldenrod,
        WearPartMonitorStatus.Shutdown => Brushes.IndianRed,
        _ => Brushes.ForestGreen
    };

    public Brush StatusForeground => Brushes.White;

    public void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(StatusText));
    }
}