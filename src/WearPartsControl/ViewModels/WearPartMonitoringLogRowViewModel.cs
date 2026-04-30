using System.Globalization;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.MonitoringLogs;

namespace WearPartsControl.ViewModels;

public sealed class WearPartMonitoringLogRowViewModel
{
    public WearPartMonitoringLogRowViewModel(WearPartMonitoringLogEntry entry)
    {
        Entry = entry;
    }

    public WearPartMonitoringLogEntry Entry { get; }

    public long Sequence => Entry.Sequence;

    public WearPartMonitoringLogLevel Level => Entry.Level;

    public WearPartMonitoringLogCategory Category => Entry.Category;

    public string TimestampText => Entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.CurrentCulture);

    public string LevelDisplayName => LocalizedText.Get($"ViewModels.WearPartMonitoringLogVm.Level{Entry.Level}");

    public string CategoryDisplayName => LocalizedText.Get($"ViewModels.WearPartMonitoringLogVm.Category{Entry.Category}");

    public string OperationName => string.IsNullOrWhiteSpace(Entry.OperationName) ? "--" : Entry.OperationName;

    public string ResourceNumber => string.IsNullOrWhiteSpace(Entry.ResourceNumber) ? "--" : Entry.ResourceNumber;

    public string Address => string.IsNullOrWhiteSpace(Entry.Address) ? "--" : Entry.Address;

    public string Message => Entry.Message;

    public string Details => string.IsNullOrWhiteSpace(Entry.Details) ? Entry.Message : Entry.Details;
}