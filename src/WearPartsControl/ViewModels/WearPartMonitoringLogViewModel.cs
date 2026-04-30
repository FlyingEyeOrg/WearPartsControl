using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.MonitoringLogs;

namespace WearPartsControl.ViewModels;

public sealed class WearPartMonitoringLogViewModel : LocalizedViewModelBase, IDisposable
{
    private const int VisiblePageSize = 200;

    private readonly IWearPartMonitoringLogPipeline _logPipeline;
    private readonly IUiDispatcher _uiDispatcher;
    private int _isRefreshScheduled;
    private int _refreshCountsRequested;
    private int _reloadRowsRequested;
    private int _resetPageRequested;
    private bool _isDisposed;
    private bool _isPaused;
    private bool _isAutoScrollEnabled = true;
    private string _keyword = string.Empty;
    private string _statusMessage = string.Empty;
    private int _pageOffset;
    private int _totalMatchingCount;
    private int _retainedCount;
    private WearPartMonitoringLogRowViewModel? _selectedEntry;
    private MonitoringLogLevelFilterOption? _selectedLevelFilter;
    private MonitoringLogCategoryFilterOption? _selectedCategoryFilter;

    public WearPartMonitoringLogViewModel(IWearPartMonitoringLogPipeline logPipeline, IUiDispatcher uiDispatcher)
    {
        _logPipeline = logPipeline;
        _uiDispatcher = uiDispatcher;

        RefreshCommand = new RelayCommand(RefreshCurrentPage);
        NewerPageCommand = new RelayCommand(LoadNewerPage, CanLoadNewerPage);
        OlderPageCommand = new RelayCommand(LoadOlderPage, CanLoadOlderPage);
        ClearCommand = new RelayCommand(Clear, CanClear);
        PauseResumeCommand = new RelayCommand(TogglePaused);
        CopySelectedCommand = new RelayCommand(RequestCopySelected, CanCopySelected);
        ExportCommand = new RelayCommand(RequestExport, CanExport);

        RebuildFilterOptions();
        LoadFirstPage();

        _logPipeline.EntriesAdded += OnEntriesAdded;
        _logPipeline.Cleared += OnPipelineCleared;
    }

    public event EventHandler<WearPartMonitoringLogExportRequestedEventArgs>? ExportRequested;

    public event EventHandler<WearPartMonitoringLogCopyRequestedEventArgs>? CopyRequested;

    public ObservableCollection<WearPartMonitoringLogRowViewModel> Entries { get; } = new();

    public ObservableCollection<MonitoringLogLevelFilterOption> LevelFilters { get; } = new();

    public ObservableCollection<MonitoringLogCategoryFilterOption> CategoryFilters { get; } = new();

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand NewerPageCommand { get; }

    public IRelayCommand OlderPageCommand { get; }

    public IRelayCommand ClearCommand { get; }

    public IRelayCommand PauseResumeCommand { get; }

    public IRelayCommand CopySelectedCommand { get; }

    public IRelayCommand ExportCommand { get; }

    public int PageSize => VisiblePageSize;

    public int CurrentPageNumber => _totalMatchingCount == 0 ? 0 : (_pageOffset / VisiblePageSize) + 1;

    public int TotalPageCount => _totalMatchingCount == 0 ? 0 : (int)Math.Ceiling(_totalMatchingCount / (double)VisiblePageSize);

    public MonitoringLogLevelFilterOption? SelectedLevelFilter
    {
        get => _selectedLevelFilter;
        set
        {
            if (SetProperty(ref _selectedLevelFilter, value))
            {
                LoadFirstPage();
            }
        }
    }

    public MonitoringLogCategoryFilterOption? SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                LoadFirstPage();
            }
        }
    }

    public WearPartMonitoringLogRowViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                CopySelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string Keyword
    {
        get => _keyword;
        set
        {
            if (SetProperty(ref _keyword, value ?? string.Empty))
            {
                LoadFirstPage();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (SetProperty(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(PauseResumeButtonText));
                UpdateStatusMessage();
            }
        }
    }

    public bool IsAutoScrollEnabled
    {
        get => _isAutoScrollEnabled;
        set => SetProperty(ref _isAutoScrollEnabled, value);
    }

    public string PauseResumeButtonText => IsPaused
        ? LocalizedText.Get("MonitoringLogControl.ResumeButton")
        : LocalizedText.Get("MonitoringLogControl.PauseButton");

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public void NotifyExportSucceeded(string filePath)
    {
        StatusMessage = LocalizedText.Format("ViewModels.WearPartMonitoringLogVm.ExportSucceeded", filePath);
    }

    public void NotifyExportCanceled()
    {
        StatusMessage = LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.ExportCanceled");
    }

    public void NotifyExportFailed(string message)
    {
        StatusMessage = LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.ExportFailedPrefix") + message;
    }

    public void NotifyCopySucceeded()
    {
        StatusMessage = LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.CopySucceeded");
    }

    public void NotifyCopyFailed(string message)
    {
        StatusMessage = LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.CopyFailedPrefix") + message;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _logPipeline.EntriesAdded -= OnEntriesAdded;
        _logPipeline.Cleared -= OnPipelineCleared;
    }

    protected override void OnLocalizationRefreshed()
    {
        var level = SelectedLevelFilter?.Level;
        var category = SelectedCategoryFilter?.Category;
        RebuildFilterOptions(level, category);
        LoadCurrentPage();
        OnPropertyChanged(nameof(PauseResumeButtonText));
        UpdateStatusMessage();
    }

    private void OnEntriesAdded(object? sender, WearPartMonitoringLogEntriesAddedEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        if (IsPaused || _pageOffset > 0)
        {
            SchedulePipelineRefresh(reloadRows: false, resetPage: false);
            return;
        }

        SchedulePipelineRefresh(reloadRows: true, resetPage: true);
    }

    private void OnPipelineCleared(object? sender, EventArgs e)
    {
        SchedulePipelineRefresh(reloadRows: true, resetPage: true);
    }

    private void SchedulePipelineRefresh(bool reloadRows, bool resetPage)
    {
        Interlocked.Exchange(ref _refreshCountsRequested, 1);

        if (reloadRows)
        {
            Interlocked.Exchange(ref _reloadRowsRequested, 1);
        }

        if (resetPage)
        {
            Interlocked.Exchange(ref _resetPageRequested, 1);
        }

        if (Interlocked.CompareExchange(ref _isRefreshScheduled, 1, 0) != 0)
        {
            return;
        }

        _ = RefreshFromPipelineAsync();
    }

    private async Task RefreshFromPipelineAsync()
    {
        try
        {
            await _uiDispatcher.RunAsync(ApplyScheduledPipelineRefresh).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _isRefreshScheduled, 0);
            if (!_isDisposed
                && (Volatile.Read(ref _refreshCountsRequested) != 0
                    || Volatile.Read(ref _reloadRowsRequested) != 0
                    || Volatile.Read(ref _resetPageRequested) != 0))
            {
                SchedulePipelineRefresh(reloadRows: false, resetPage: false);
            }
        }
    }

    private void ApplyScheduledPipelineRefresh()
    {
        if (_isDisposed)
        {
            return;
        }

        var reloadRows = Interlocked.Exchange(ref _reloadRowsRequested, 0) != 0;
        var resetPage = Interlocked.Exchange(ref _resetPageRequested, 0) != 0;
    var refreshCounts = Interlocked.Exchange(ref _refreshCountsRequested, 0) != 0;

        if (resetPage)
        {
            _pageOffset = 0;
        }

        if (reloadRows)
        {
            LoadCurrentPage();
            return;
        }

        if (refreshCounts)
        {
            RefreshCountsOnly();
        }
    }

    private void RefreshCurrentPage()
    {
        LoadCurrentPage();
    }

    private void LoadFirstPage()
    {
        _pageOffset = 0;
        LoadCurrentPage();
    }

    private void LoadNewerPage()
    {
        if (!CanLoadNewerPage())
        {
            return;
        }

        _pageOffset = Math.Max(0, _pageOffset - VisiblePageSize);
        LoadCurrentPage();
    }

    private bool CanLoadNewerPage() => _pageOffset > 0;

    private void LoadOlderPage()
    {
        if (!CanLoadOlderPage())
        {
            return;
        }

        _pageOffset += VisiblePageSize;
        LoadCurrentPage();
    }

    private bool CanLoadOlderPage() => _pageOffset + VisiblePageSize < _totalMatchingCount;

    private void LoadCurrentPage()
    {
        var selectedSequence = SelectedEntry?.Sequence;
        var page = QueryPage(_pageOffset, VisiblePageSize);
        if (_pageOffset >= page.TotalCount && _pageOffset > 0)
        {
            _pageOffset = Math.Max(0, (Math.Max(0, page.TotalCount - 1) / VisiblePageSize) * VisiblePageSize);
            page = QueryPage(_pageOffset, VisiblePageSize);
        }

        _totalMatchingCount = page.TotalCount;
        _retainedCount = page.RetainedCount;
        Entries.Clear();
        foreach (var entry in page.Entries)
        {
            Entries.Add(new WearPartMonitoringLogRowViewModel(entry));
        }

        SelectedEntry = selectedSequence is null
            ? null
            : Entries.FirstOrDefault(entry => entry.Sequence == selectedSequence.Value);

        RaisePagePropertiesChanged();
        UpdateCommands();
        UpdateStatusMessage();
    }

    private void RefreshCountsOnly()
    {
        var page = QueryPage(_pageOffset, 1);
        _totalMatchingCount = page.TotalCount;
        _retainedCount = page.RetainedCount;
        RaisePagePropertiesChanged();
        UpdateCommands();
        UpdateStatusMessage();
    }

    private WearPartMonitoringLogPage QueryPage(int offset, int limit)
    {
        return _logPipeline.Query(new WearPartMonitoringLogQuery(
            SelectedLevelFilter?.Level,
            SelectedCategoryFilter?.Category,
            Keyword,
            offset,
            limit));
    }

    private void Clear()
    {
        _logPipeline.Clear();
        _pageOffset = 0;
        _totalMatchingCount = 0;
        _retainedCount = 0;
        Entries.Clear();
        SelectedEntry = null;
        RaisePagePropertiesChanged();
        UpdateCommands();
        UpdateStatusMessage();
    }

    private bool CanClear() => _retainedCount > 0;

    private void TogglePaused()
    {
        IsPaused = !IsPaused;
        if (!IsPaused)
        {
            LoadFirstPage();
            return;
        }

        RefreshCountsOnly();
    }

    private void RequestCopySelected()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        CopyRequested?.Invoke(this, new WearPartMonitoringLogCopyRequestedEventArgs(FormatEntryForCopy(SelectedEntry.Entry)));
    }

    private bool CanCopySelected() => SelectedEntry is not null;

    private void RequestExport()
    {
        var page = QueryPage(0, _logPipeline.Capacity);
        if (page.TotalCount == 0)
        {
            return;
        }

        var fileName = LocalizedText.Format("ViewModels.WearPartMonitoringLogVm.ExportFileName", DateTime.Now);
        ExportRequested?.Invoke(this, new WearPartMonitoringLogExportRequestedEventArgs(fileName, BuildCsv(page.Entries)));
    }

    private bool CanExport() => _totalMatchingCount > 0;

    private void RebuildFilterOptions(WearPartMonitoringLogLevel? selectedLevel = null, WearPartMonitoringLogCategory? selectedCategory = null)
    {
        LevelFilters.Clear();
        LevelFilters.Add(new MonitoringLogLevelFilterOption(LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.FilterAllLevels"), null));
        LevelFilters.Add(new MonitoringLogLevelFilterOption(LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.LevelDebug"), WearPartMonitoringLogLevel.Debug));
        LevelFilters.Add(new MonitoringLogLevelFilterOption(LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.LevelInformation"), WearPartMonitoringLogLevel.Information));
        LevelFilters.Add(new MonitoringLogLevelFilterOption(LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.LevelWarning"), WearPartMonitoringLogLevel.Warning));
        LevelFilters.Add(new MonitoringLogLevelFilterOption(LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.LevelError"), WearPartMonitoringLogLevel.Error));
        _selectedLevelFilter = LevelFilters.First(option => option.Level == selectedLevel);
        OnPropertyChanged(nameof(SelectedLevelFilter));

        CategoryFilters.Clear();
        CategoryFilters.Add(new MonitoringLogCategoryFilterOption(LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.FilterAllCategories"), null));
        CategoryFilters.Add(new MonitoringLogCategoryFilterOption(LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.CategoryService"), WearPartMonitoringLogCategory.Service));
        CategoryFilters.Add(new MonitoringLogCategoryFilterOption(LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.CategoryPlc"), WearPartMonitoringLogCategory.Plc));
        CategoryFilters.Add(new MonitoringLogCategoryFilterOption(LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.CategoryCom"), WearPartMonitoringLogCategory.Com));
        _selectedCategoryFilter = CategoryFilters.First(option => option.Category == selectedCategory);
        OnPropertyChanged(nameof(SelectedCategoryFilter));
    }

    private void RaisePagePropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentPageNumber));
        OnPropertyChanged(nameof(TotalPageCount));
    }

    private void UpdateCommands()
    {
        NewerPageCommand.NotifyCanExecuteChanged();
        OlderPageCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        CopySelectedCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    private void UpdateStatusMessage()
    {
        var key = IsPaused
            ? "ViewModels.WearPartMonitoringLogVm.StatusPaused"
            : "ViewModels.WearPartMonitoringLogVm.StatusLive";

        StatusMessage = LocalizedText.Format(
            key,
            Entries.Count,
            CurrentPageNumber,
            TotalPageCount,
            _totalMatchingCount,
            _retainedCount,
            _logPipeline.Capacity);
    }

    private string BuildCsv(IEnumerable<WearPartMonitoringLogEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine(LocalizedText.Get("ViewModels.WearPartMonitoringLogVm.ExportCsvHeader"));
        foreach (var entry in entries.OrderBy(entry => entry.Sequence))
        {
            var row = new WearPartMonitoringLogRowViewModel(entry);
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(row.TimestampText),
                Csv(row.LevelDisplayName),
                Csv(row.CategoryDisplayName),
                Csv(row.OperationName),
                Csv(row.ResourceNumber),
                Csv(row.Address),
                Csv(row.Message),
                Csv(row.Details)
            }));
        }

        return builder.ToString();
    }

    private static string FormatEntryForCopy(WearPartMonitoringLogEntry entry)
    {
        var row = new WearPartMonitoringLogRowViewModel(entry);
        return string.Join(Environment.NewLine, new[]
        {
            $"{LocalizedText.Get("MonitoringLogControl.TimeHeader")}: {row.TimestampText}",
            $"{LocalizedText.Get("MonitoringLogControl.LevelHeader")}: {row.LevelDisplayName}",
            $"{LocalizedText.Get("MonitoringLogControl.CategoryHeader")}: {row.CategoryDisplayName}",
            $"{LocalizedText.Get("MonitoringLogControl.OperationHeader")}: {row.OperationName}",
            $"{LocalizedText.Get("MonitoringLogControl.ResourceHeader")}: {row.ResourceNumber}",
            $"{LocalizedText.Get("MonitoringLogControl.AddressHeader")}: {row.Address}",
            $"{LocalizedText.Get("MonitoringLogControl.MessageHeader")}: {row.Message}",
            $"{LocalizedText.Get("MonitoringLogControl.DetailsLabel")}: {row.Details}"
        });
    }

    private static string Csv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}