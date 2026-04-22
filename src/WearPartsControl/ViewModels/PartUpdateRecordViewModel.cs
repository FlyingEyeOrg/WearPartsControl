using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class PartUpdateRecordViewModel : ObservableObject
{
    private const int DefaultPageSize = 20;
    private static readonly int[] SupportedPageSizes = [10, 20, 50, 100];

    private readonly IClientAppInfoService _clientAppInfoService;
    private readonly IWearPartManagementService _wearPartManagementService;
    private readonly IWearPartReplacementService _wearPartReplacementService;
    private readonly IUiBusyService _uiBusyService;
    private readonly List<WearPartReplacementRecord> _allRecords = new();
    private readonly List<WearPartReplacementRecord> _filteredRecords = new();
    private readonly List<WearPartDefinition> _allDefinitions = new();
    private Guid _clientAppConfigurationId;
    private string _resourceNumber = string.Empty;
    private WearPartDefinition? _selectedDefinition;
    private bool _isBusy;
    private bool _isInitialized;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _selectedPageSize = DefaultPageSize;
    private string _requestedPageNumber = "1";
    private string _statusMessage = LocalizedText.Get("ViewModels.PartUpdateRecordVm.PromptLoadCurrent");

    public PartUpdateRecordViewModel(
        IClientAppInfoService clientAppInfoService,
        IWearPartManagementService wearPartManagementService,
        IWearPartReplacementService wearPartReplacementService,
        IUiBusyService uiBusyService)
    {
        _clientAppInfoService = clientAppInfoService;
        _wearPartManagementService = wearPartManagementService;
        _wearPartReplacementService = wearPartReplacementService;
        _uiBusyService = uiBusyService;

        QueryCommand = new RelayCommand(ApplyFilter, CanQuery);
        ExportCommand = new RelayCommand(RequestExport, CanExport);
        PreviousPageCommand = new RelayCommand(MoveToPreviousPage, CanMoveToPreviousPage);
        NextPageCommand = new RelayCommand(MoveToNextPage, CanMoveToNextPage);
        GoToPageCommand = new RelayCommand(MoveToRequestedPage, CanMoveToRequestedPage);
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(CancellationToken.None), CanRefresh);

        foreach (var pageSize in SupportedPageSizes)
        {
            PageSizeOptions.Add(pageSize);
        }
    }

    public event EventHandler<PartUpdateRecordExportRequestedEventArgs>? ExportRequested;

    public ObservableCollection<WearPartDefinition> Definitions { get; } = new();

    public ObservableCollection<WearPartReplacementRecord> Records { get; } = new();

    public ObservableCollection<int> PageSizeOptions { get; } = new();

    public IRelayCommand QueryCommand { get; }

    public IRelayCommand ExportCommand { get; }

    public IRelayCommand PreviousPageCommand { get; }

    public IRelayCommand NextPageCommand { get; }

    public IRelayCommand GoToPageCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public string ResourceNumber
    {
        get => _resourceNumber;
        private set => SetProperty(ref _resourceNumber, value);
    }

    public WearPartDefinition? SelectedDefinition
    {
        get => _selectedDefinition;
        set => SetProperty(ref _selectedDefinition, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                QueryCommand.NotifyCanExecuteChanged();
                ExportCommand.NotifyCanExecuteChanged();
                PreviousPageCommand.NotifyCanExecuteChanged();
                NextPageCommand.NotifyCanExecuteChanged();
                GoToPageCommand.NotifyCanExecuteChanged();
                RefreshCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public int TotalPages
    {
        get => _totalPages;
        private set => SetProperty(ref _totalPages, value);
    }

    public int SelectedPageSize
    {
        get => _selectedPageSize;
        set
        {
            if (SetProperty(ref _selectedPageSize, value))
            {
                CurrentPage = 1;
                RequestedPageNumber = "1";
                RecalculatePagingAndReload();
            }
        }
    }

    public string RequestedPageNumber
    {
        get => _requestedPageNumber;
        set
        {
            if (SetProperty(ref _requestedPageNumber, value))
            {
                GoToPageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(true);
        _isInitialized = true;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = LocalizedText.Get("ViewModels.PartUpdateRecordVm.Loading");
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.PartUpdateRecordVm.Loading"));

        try
        {
            var clientInfo = await _clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(true);
            _clientAppConfigurationId = clientInfo.Id ?? Guid.Empty;
            ResourceNumber = clientInfo.ResourceNumber?.Trim() ?? string.Empty;

            Definitions.Clear();
            _allDefinitions.Clear();
            _allRecords.Clear();
            _filteredRecords.Clear();
            Records.Clear();
            CurrentPage = 1;
            TotalPages = 1;
            SelectedPageSize = DefaultPageSize;
            RequestedPageNumber = "1";

            if (_clientAppConfigurationId == Guid.Empty || string.IsNullOrWhiteSpace(ResourceNumber))
            {
                SelectedDefinition = null;
                StatusMessage = LocalizedText.Get("ViewModels.PartUpdateRecordVm.ResourceNumberMissing");
                return;
            }

            var definitionsTask = _wearPartManagementService.GetDefinitionsByClientAppConfigurationAsync(_clientAppConfigurationId, cancellationToken);
            var historyTask = _wearPartReplacementService.GetReplacementHistoryAsync(_clientAppConfigurationId, cancellationToken);
            await Task.WhenAll(definitionsTask, historyTask).ConfigureAwait(true);

            var definitions = definitionsTask.Result.OrderBy(x => x.PartName, StringComparer.OrdinalIgnoreCase).ToArray();
            _allDefinitions.AddRange(definitions);
            foreach (var definition in definitions)
            {
                Definitions.Add(definition);
            }

            var selectedDefinitionId = SelectedDefinition?.Id;
            SelectedDefinition = selectedDefinitionId is null
                ? null
                : Definitions.FirstOrDefault(x => x.Id == selectedDefinitionId.Value);

            _allRecords.AddRange(historyTask.Result.OrderByDescending(x => x.ReplacedAt));
            ApplyFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void NotifyExportSucceeded(string filePath)
    {
        StatusMessage = LocalizedText.Format("ViewModels.PartUpdateRecordVm.ExportSucceeded", filePath);
    }

    public void NotifyExportCanceled()
    {
        StatusMessage = LocalizedText.Get("ViewModels.PartUpdateRecordVm.ExportCanceled");
    }

    public void NotifyExportFailed(string message)
    {
        StatusMessage = LocalizedText.Get("ViewModels.PartUpdateRecordVm.ExportFailedPrefix") + message;
    }

    private bool CanQuery() => !IsBusy;

    private bool CanExport() => !IsBusy && _filteredRecords.Count > 0;

    private bool CanMoveToPreviousPage() => !IsBusy && CurrentPage > 1;

    private bool CanMoveToNextPage() => !IsBusy && CurrentPage < TotalPages;

    private bool CanMoveToRequestedPage() => !IsBusy;

    private bool CanRefresh() => !IsBusy;

    private void ApplyFilter()
    {
        _filteredRecords.Clear();
        var selectedDefinitionId = SelectedDefinition?.Id;
        var records = selectedDefinitionId is null
            ? _allRecords
            : _allRecords.Where(x => x.WearPartDefinitionId == selectedDefinitionId.Value).ToList();

        _filteredRecords.AddRange(records);

        RecalculatePagingAndReload();
    }

    private void MoveToPreviousPage()
    {
        if (CurrentPage <= 1)
        {
            return;
        }

        CurrentPage--;
        RequestedPageNumber = CurrentPage.ToString();
        LoadCurrentPage();
    }

    private void MoveToNextPage()
    {
        if (CurrentPage >= TotalPages)
        {
            return;
        }

        CurrentPage++;
        RequestedPageNumber = CurrentPage.ToString();
        LoadCurrentPage();
    }

    private void MoveToRequestedPage()
    {
        if (!int.TryParse(RequestedPageNumber?.Trim(), out var pageNumber) || pageNumber <= 0)
        {
            StatusMessage = LocalizedText.Get("ViewModels.PartUpdateRecordVm.InvalidPageNumber");
            return;
        }

        CurrentPage = Math.Min(pageNumber, TotalPages);
        RequestedPageNumber = CurrentPage.ToString();
        LoadCurrentPage();
    }

    private void RecalculatePagingAndReload()
    {
        TotalPages = Math.Max(1, (int)Math.Ceiling(_filteredRecords.Count / (double)SelectedPageSize));
        CurrentPage = Math.Min(Math.Max(1, CurrentPage), TotalPages);
        RequestedPageNumber = CurrentPage.ToString();
        LoadCurrentPage();
        UpdateStatusMessage();

        ExportCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        GoToPageCommand.NotifyCanExecuteChanged();
    }

    private void LoadCurrentPage()
    {
        Records.Clear();
        foreach (var record in _filteredRecords.Skip((CurrentPage - 1) * SelectedPageSize).Take(SelectedPageSize))
        {
            Records.Add(record);
        }

        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void UpdateStatusMessage()
    {
        if (string.IsNullOrWhiteSpace(ResourceNumber))
        {
            return;
        }

        if (_allRecords.Count == 0)
        {
            StatusMessage = LocalizedText.Format("ViewModels.PartUpdateRecordVm.RecordsEmpty", ResourceNumber);
            return;
        }

        if (SelectedDefinition is null)
        {
            StatusMessage = LocalizedText.Format("ViewModels.PartUpdateRecordVm.RecordsLoaded", ResourceNumber, _allRecords.Count);
            return;
        }

        StatusMessage = LocalizedText.Format("ViewModels.PartUpdateRecordVm.RecordsFiltered", _filteredRecords.Count);
    }

    private void RequestExport()
    {
        var content = BuildCsvContent(_filteredRecords);
        var fileName = LocalizedText.Format("ViewModels.PartUpdateRecordVm.ExportFileName", ResourceNumber, DateTime.Now);
        ExportRequested?.Invoke(this, new PartUpdateRecordExportRequestedEventArgs(fileName, content));
    }

    private static string BuildCsvContent(IEnumerable<WearPartReplacementRecord> records)
    {
        var builder = new StringBuilder();
        builder.AppendLine("名称,更换原因,旧编码,新编码,当前寿命,预警寿命,停机寿命,更换时间,操作员,备注");

        foreach (var record in records)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Escape(record.PartName),
                Escape(record.ReasonDisplayName),
                Escape(record.OldBarcode ?? string.Empty),
                Escape(record.NewBarcode),
                Escape(record.CurrentValue),
                Escape(record.WarningValue),
                Escape(record.ShutdownValue),
                Escape(record.ReplacedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                Escape(record.OperatorWorkNumber),
                Escape(record.ReplacementMessage)
            }));
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

public sealed class PartUpdateRecordExportRequestedEventArgs : EventArgs
{
    public PartUpdateRecordExportRequestedEventArgs(string suggestedFileName, string content)
    {
        SuggestedFileName = suggestedFileName;
        Content = content;
    }

    public string SuggestedFileName { get; }

    public string Content { get; }
}