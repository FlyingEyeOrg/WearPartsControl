using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class PartManagementViewModel : ObservableObject
{
    private readonly IClientAppInfoService _clientAppInfoService;
    private readonly IWearPartManagementService _wearPartManagementService;
    private readonly IUiBusyService _uiBusyService;
    private readonly List<WearPartDefinition> _allDefinitions = new();
    private Guid _clientAppConfigurationId;
    private string _resourceNumber = string.Empty;
    private string _partNameFilter = string.Empty;
    private WearPartDefinition? _selectedDefinition;
    private bool _isBusy;
    private bool _isInitialized;
    private string _statusMessage = "请先加载当前设备的易损件。";

    public PartManagementViewModel(
        IClientAppInfoService clientAppInfoService,
        IWearPartManagementService wearPartManagementService,
        IUiBusyService uiBusyService)
    {
        _clientAppInfoService = clientAppInfoService;
        _wearPartManagementService = wearPartManagementService;
        _uiBusyService = uiBusyService;

        SearchCommand = new RelayCommand(ApplyFilter);
        RefreshCommand = new AsyncRelayCommand(RefreshCommandAsync, CanRefresh);
        AddCommand = new RelayCommand(OpenAddDialog, CanAdd);
        EditCommand = new RelayCommand(OpenEditDialog, CanEdit);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, CanDelete);
    }

    public event EventHandler? AddRequested;

    public event EventHandler<WearPartDefinition>? EditRequested;

    public ObservableCollection<WearPartDefinition> Definitions { get; } = new();

    public IRelayCommand SearchCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand AddCommand { get; }

    public IRelayCommand EditCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    public Guid ClientAppConfigurationId => _clientAppConfigurationId;

    public string ResourceNumber
    {
        get => _resourceNumber;
        private set => SetProperty(ref _resourceNumber, value);
    }

    public string PartNameFilter
    {
        get => _partNameFilter;
        set => SetProperty(ref _partNameFilter, value);
    }

    public WearPartDefinition? SelectedDefinition
    {
        get => _selectedDefinition;
        set
        {
            if (SetProperty(ref _selectedDefinition, value))
            {
                EditCommand.NotifyCanExecuteChanged();
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                RefreshCommand.NotifyCanExecuteChanged();
                AddCommand.NotifyCanExecuteChanged();
                EditCommand.NotifyCanExecuteChanged();
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
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

    public Task RefreshAsync()
    {
        return RefreshAsync(CancellationToken.None);
    }

    private Task RefreshCommandAsync()
    {
        return RefreshAsync();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var enteredAt = DateTimeOffset.UtcNow;
        IsBusy = true;
        StatusMessage = "正在加载易损件列表...";
        using var _ = _uiBusyService.Enter();

        try
        {
            var clientInfo = await _clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(true);
            _clientAppConfigurationId = clientInfo.Id ?? Guid.Empty;
            ResourceNumber = clientInfo.ResourceNumber?.Trim() ?? string.Empty;

            _allDefinitions.Clear();
            Definitions.Clear();
            SelectedDefinition = null;

            if (_clientAppConfigurationId == Guid.Empty || string.IsNullOrWhiteSpace(ResourceNumber))
            {
                StatusMessage = "当前客户端未配置资源号，无法管理易损件。";
                return;
            }

            var definitions = await _wearPartManagementService
                .GetDefinitionsByClientAppConfigurationAsync(_clientAppConfigurationId, cancellationToken)
                .ConfigureAwait(true);

            _allDefinitions.AddRange(definitions.OrderBy(x => x.PartName, StringComparer.OrdinalIgnoreCase));
            ApplyFilter();
            await EnsureMinimumBusyDurationAsync(enteredAt, cancellationToken).ConfigureAwait(true);
            StatusMessage = _allDefinitions.Count == 0
                ? $"资源号 {ResourceNumber} 当前暂无易损件定义。"
                : $"已加载资源号 {ResourceNumber} 的 {_allDefinitions.Count} 条易损件定义。";
        }
        catch (Exception ex)
        {
            await EnsureMinimumBusyDurationAsync(enteredAt, cancellationToken).ConfigureAwait(true);
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRefresh()
    {
        return !IsBusy;
    }

    private bool CanAdd()
    {
        return !IsBusy && _clientAppConfigurationId != Guid.Empty && !string.IsNullOrWhiteSpace(ResourceNumber);
    }

    private bool CanEdit()
    {
        return !IsBusy && SelectedDefinition is not null;
    }

    private bool CanDelete()
    {
        return !IsBusy && SelectedDefinition is not null;
    }

    private void OpenAddDialog()
    {
        AddRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenEditDialog()
    {
        if (SelectedDefinition is null)
        {
            return;
        }

        EditRequested?.Invoke(this, SelectedDefinition);
    }

    private async Task DeleteAsync()
    {
        if (SelectedDefinition is null)
        {
            return;
        }

        var definition = SelectedDefinition;
        var result = MessageBox.Show(
            $"确认删除易损件“{definition.PartName}”吗？",
            "删除确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var enteredAt = DateTimeOffset.UtcNow;
        IsBusy = true;
        StatusMessage = "正在删除易损件...";
        using var _ = _uiBusyService.Enter();

        try
        {
            await _wearPartManagementService.DeleteDefinitionAsync(definition.Id).ConfigureAwait(true);
            await RefreshAsync(CancellationToken.None).ConfigureAwait(true);
            await EnsureMinimumBusyDurationAsync(enteredAt, CancellationToken.None).ConfigureAwait(true);
            StatusMessage = $"易损件 {definition.PartName} 已删除。";
        }
        catch (Exception ex)
        {
            await EnsureMinimumBusyDurationAsync(enteredAt, CancellationToken.None).ConfigureAwait(true);
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var keyword = PartNameFilter?.Trim();
        var filtered = string.IsNullOrWhiteSpace(keyword)
            ? _allDefinitions
            : _allDefinitions.Where(x => x.PartName.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        Definitions.Clear();
        foreach (var definition in filtered)
        {
            Definitions.Add(definition);
        }

        if (SelectedDefinition is not null && !Definitions.Any(x => x.Id == SelectedDefinition.Id))
        {
            SelectedDefinition = null;
        }

        if (!string.IsNullOrWhiteSpace(ResourceNumber))
        {
            StatusMessage = Definitions.Count == 0
                ? $"资源号 {ResourceNumber} 没有匹配“{keyword}”的易损件。"
                : $"当前显示 {Definitions.Count} 条易损件定义。";
        }
    }

    private async Task EnsureMinimumBusyDurationAsync(DateTimeOffset busyEnteredAt, CancellationToken cancellationToken)
    {
        var elapsed = DateTimeOffset.UtcNow - busyEnteredAt;
        var remaining = _uiBusyService.MinimumBusyDuration - elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(remaining, cancellationToken).ConfigureAwait(true);
    }
}