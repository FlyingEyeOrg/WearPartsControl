using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ViewModels;

public sealed class KdlRecipeManagementViewModel : LocalizedViewModelBase
{
    private readonly IKdlRecipeManagementService _kdlRecipeManagementService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IUiBusyService _uiBusyService;
    private readonly IAppDialogService _dialogService;
    private readonly List<SelectableItem<KdlRecipeDefinition>> _allRecipes = new();
    private SelectableItem<KdlRecipeDefinition>? _selectedRecipeRow;
    private string _recipeName = string.Empty;
    private string _lowerLimit = string.Empty;
    private string _upperLimit = string.Empty;
    private string _keyword = string.Empty;
    private string _statusMessage = LocalizedText.Get("ViewModels.KdlRecipeManagementVm.PromptLoadCurrent");
    private bool _areAllRecipesChecked;
    private bool _isBusy;
    private bool _isInitialized;
    private bool _isUpdatingCheckedState;
    private Func<string>? _statusMessageFactory;

    public KdlRecipeManagementViewModel(
        IKdlRecipeManagementService kdlRecipeManagementService,
        IUiDispatcher uiDispatcher,
        IUiBusyService uiBusyService,
        IAppDialogService dialogService)
    {
        _kdlRecipeManagementService = kdlRecipeManagementService;
        _uiDispatcher = uiDispatcher;
        _uiBusyService = uiBusyService;
        _dialogService = dialogService;

        SearchCommand = new RelayCommand(ApplyFilter);
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(CancellationToken.None), () => !IsBusy);
        NewCommand = new AsyncRelayCommand(() => CreateAsync(CancellationToken.None), CanCreate);
        EditCommand = new AsyncRelayCommand(() => EditAsync(CancellationToken.None), CanEdit);
        SetCurrentCommand = new AsyncRelayCommand(() => SetCurrentAsync(CancellationToken.None), CanSetCurrent);
        DeleteCommand = new AsyncRelayCommand(() => DeleteAsync(CancellationToken.None), CanDelete);
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.KdlRecipeManagementVm.PromptLoadCurrent"));
    }

    public ObservableCollection<SelectableItem<KdlRecipeDefinition>> Recipes { get; } = new();

    public IRelayCommand SearchCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand NewCommand { get; }

    public IAsyncRelayCommand EditCommand { get; }

    public IAsyncRelayCommand SetCurrentCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    public SelectableItem<KdlRecipeDefinition>? SelectedRecipeRow
    {
        get => _selectedRecipeRow;
        set
        {
            if (!SetProperty(ref _selectedRecipeRow, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedRecipe));

            var recipe = value?.Item;
            if (recipe is null)
            {
                RecipeName = string.Empty;
                LowerLimit = string.Empty;
                UpperLimit = string.Empty;
            }
            else
            {
                RecipeName = recipe.Name;
                LowerLimit = recipe.LowerLimit.ToString(CultureInfo.InvariantCulture);
                UpperLimit = recipe.UpperLimit.ToString(CultureInfo.InvariantCulture);
                SetLocalizedStatusMessage(() => recipe.IsCurrent
                    ? LocalizedText.Format("ViewModels.KdlRecipeManagementVm.EditingCurrent", recipe.Name)
                    : LocalizedText.Format("ViewModels.KdlRecipeManagementVm.Editing", recipe.Name));
            }

            NewCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            SetCurrentCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    public KdlRecipeDefinition? SelectedRecipe
    {
        get => SelectedRecipeRow?.Item;
        set => SelectedRecipeRow = value is null ? null : Recipes.FirstOrDefault(x => x.Item.Id == value.Id);
    }

    public bool AreAllRecipesChecked
    {
        get => _areAllRecipesChecked;
        set
        {
            if (!SetProperty(ref _areAllRecipesChecked, value))
            {
                return;
            }

            if (_isUpdatingCheckedState)
            {
                return;
            }

            foreach (var recipe in Recipes)
            {
                recipe.IsChecked = value;
            }

            DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    public string RecipeName
    {
        get => _recipeName;
        set
        {
            if (SetProperty(ref _recipeName, value))
            {
                NewCommand.NotifyCanExecuteChanged();
                EditCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string LowerLimit
    {
        get => _lowerLimit;
        set
        {
            if (SetProperty(ref _lowerLimit, value))
            {
                NewCommand.NotifyCanExecuteChanged();
                EditCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string UpperLimit
    {
        get => _upperLimit;
        set
        {
            if (SetProperty(ref _upperLimit, value))
            {
                NewCommand.NotifyCanExecuteChanged();
                EditCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string Keyword
    {
        get => _keyword;
        set => SetProperty(ref _keyword, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                NewCommand.NotifyCanExecuteChanged();
                EditCommand.NotifyCanExecuteChanged();
                SetCurrentCommand.NotifyCanExecuteChanged();
                DeleteCommand.NotifyCanExecuteChanged();
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
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.KdlRecipeManagementVm.Loading"));
        using var _ = _uiBusyService.Enter(StatusMessage);
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            await ReloadRecipesAsync(cancellationToken, preserveSelection: true).ConfigureAwait(true);

            var currentRecipeName = _allRecipes.FirstOrDefault(item => item.Item.IsCurrent)?.Item.Name;
            SetLocalizedStatusMessage(() => _allRecipes.Count == 0
                ? LocalizedText.Get("ViewModels.KdlRecipeManagementVm.Empty")
                : string.IsNullOrWhiteSpace(currentRecipeName)
                    ? LocalizedText.Format("ViewModels.KdlRecipeManagementVm.LoadedWithoutCurrent", _allRecipes.Count)
                    : LocalizedText.Format("ViewModels.KdlRecipeManagementVm.Loaded", _allRecipes.Count, currentRecipeName));
        }
        catch (Exception ex)
        {
            SetRawStatusMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        if (!CanCreate())
        {
            return;
        }

        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.KdlRecipeManagementVm.Creating"));
        using var _ = _uiBusyService.Enter(StatusMessage);
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            var saved = await _kdlRecipeManagementService.CreateAsync(BuildDefinition(), cancellationToken).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(Keyword))
            {
                Keyword = string.Empty;
            }

            await ReloadRecipesAsync(cancellationToken, saved.Id).ConfigureAwait(true);
            SelectedRecipe = Recipes.FirstOrDefault(x => x.Item.Id == saved.Id)?.Item;
            SetLocalizedStatusMessage(() => saved.IsCurrent
                ? LocalizedText.Format("ViewModels.KdlRecipeManagementVm.CreatedAndSelected", saved.Name)
                : LocalizedText.Format("ViewModels.KdlRecipeManagementVm.Created", saved.Name));
        }
        catch (Exception ex)
        {
            SetRawStatusMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EditAsync(CancellationToken cancellationToken)
    {
        if (!CanEdit())
        {
            return;
        }

        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.KdlRecipeManagementVm.Updating"));
        using var _ = _uiBusyService.Enter(StatusMessage);
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            var saved = await _kdlRecipeManagementService.UpdateAsync(BuildDefinition(SelectedRecipe!.Id), cancellationToken).ConfigureAwait(true);
            await ReloadRecipesAsync(cancellationToken, saved.Id).ConfigureAwait(true);
            SelectedRecipe = Recipes.FirstOrDefault(x => x.Item.Id == saved.Id)?.Item;
            SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.KdlRecipeManagementVm.Updated", saved.Name));
        }
        catch (Exception ex)
        {
            SetRawStatusMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SetCurrentAsync(CancellationToken cancellationToken)
    {
        if (!CanSetCurrent())
        {
            return;
        }

        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.KdlRecipeManagementVm.SettingCurrent"));
        using var _ = _uiBusyService.Enter(StatusMessage);
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            await _kdlRecipeManagementService.SetCurrentAsync(SelectedRecipe!.Id, cancellationToken).ConfigureAwait(true);
            await ReloadRecipesAsync(cancellationToken, SelectedRecipe.Id).ConfigureAwait(true);
            SelectedRecipe = Recipes.FirstOrDefault(x => x.Item.Id == SelectedRecipe.Id)?.Item;
            SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.KdlRecipeManagementVm.CurrentSet", SelectedRecipe!.Name));
        }
        catch (Exception ex)
        {
            SetRawStatusMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        var recipesToDelete = Recipes.Where(x => x.IsChecked).Select(x => x.Item).ToList();
        if (recipesToDelete.Count == 0 && SelectedRecipe is not null)
        {
            recipesToDelete.Add(SelectedRecipe);
        }

        if (recipesToDelete.Count == 0)
        {
            return;
        }

        var isBatchDelete = recipesToDelete.Count > 1;
        var result = _dialogService.ShowMessage(
            isBatchDelete
                ? LocalizedText.Format("ViewModels.KdlRecipeManagementVm.DeleteMultipleConfirmationMessage", recipesToDelete.Count)
                : LocalizedText.Format("ViewModels.KdlRecipeManagementVm.DeleteConfirmationMessage", recipesToDelete[0].Name),
            LocalizedText.Get("ViewModels.KdlRecipeManagementVm.DeleteConfirmationTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.KdlRecipeManagementVm.Deleting"));
        using var _ = _uiBusyService.Enter(StatusMessage);
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            foreach (var recipe in recipesToDelete)
            {
                await _kdlRecipeManagementService.DeleteAsync(recipe.Id, cancellationToken).ConfigureAwait(true);
            }

            await ReloadRecipesAsync(cancellationToken, selectedId: null).ConfigureAwait(true);
            ClearEditor();
            SetLocalizedStatusMessage(() => isBatchDelete
                ? LocalizedText.Format("ViewModels.KdlRecipeManagementVm.DeletedMultiple", recipesToDelete.Count)
                : LocalizedText.Format("ViewModels.KdlRecipeManagementVm.Deleted", recipesToDelete[0].Name));
        }
        catch (Exception ex)
        {
            SetRawStatusMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var keyword = Keyword?.Trim();
        var filtered = string.IsNullOrWhiteSpace(keyword)
            ? _allRecipes
            : _allRecipes.Where(x => x.Item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        Recipes.Clear();
        foreach (var recipe in filtered)
        {
            Recipes.Add(recipe);
        }

        if (SelectedRecipe is not null)
        {
            SelectedRecipe = Recipes.FirstOrDefault(x => x.Item.Id == SelectedRecipe.Id)?.Item;
        }

        UpdateCheckedSummaryState();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private async Task ReloadRecipesAsync(CancellationToken cancellationToken, bool preserveSelection)
    {
        await ReloadRecipesAsync(cancellationToken, preserveSelection ? SelectedRecipe?.Id : null).ConfigureAwait(true);
    }

    private async Task ReloadRecipesAsync(CancellationToken cancellationToken, Guid? selectedId)
    {
        var state = await _kdlRecipeManagementService.GetAsync(cancellationToken).ConfigureAwait(true);
        ClearRecipeRows();
        foreach (var recipe in state.Recipes)
        {
            var row = new SelectableItem<KdlRecipeDefinition>(recipe);
            row.PropertyChanged += OnRecipeRowPropertyChanged;
            _allRecipes.Add(row);
        }

        ApplyFilter();

        if (selectedId.HasValue)
        {
            SelectedRecipe = Recipes.FirstOrDefault(x => x.Item.Id == selectedId.Value)?.Item;
        }
    }

    private KdlRecipeDefinition BuildDefinition(Guid? id = null)
    {
        if (!TryParseLimit(LowerLimit, out var lowerLimit))
        {
            throw new UserFriendlyException(LocalizedText.Get("ViewModels.KdlRecipeManagementVm.LowerLimitInvalid"));
        }

        if (!TryParseLimit(UpperLimit, out var upperLimit))
        {
            throw new UserFriendlyException(LocalizedText.Get("ViewModels.KdlRecipeManagementVm.UpperLimitInvalid"));
        }

        return new KdlRecipeDefinition
        {
            Id = id ?? Guid.NewGuid(),
            Name = RecipeName,
            LowerLimit = lowerLimit,
            UpperLimit = upperLimit
        };
    }

    private bool CanCreate()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(RecipeName) && TryParseLimit(LowerLimit, out _) && TryParseLimit(UpperLimit, out _);
    }

    private bool CanEdit()
    {
        return !IsBusy && SelectedRecipe is not null && !string.IsNullOrWhiteSpace(RecipeName) && TryParseLimit(LowerLimit, out _) && TryParseLimit(UpperLimit, out _);
    }

    private bool CanSetCurrent()
    {
        return !IsBusy && SelectedRecipe is not null && !SelectedRecipe.IsCurrent;
    }

    private bool CanDelete()
    {
        return !IsBusy && (SelectedRecipe is not null || Recipes.Any(x => x.IsChecked));
    }

    protected override void OnLocalizationRefreshed()
    {
        if (_statusMessageFactory is not null)
        {
            StatusMessage = _statusMessageFactory();
        }
    }

    private void ClearEditor()
    {
        SelectedRecipe = null;
        RecipeName = string.Empty;
        LowerLimit = string.Empty;
        UpperLimit = string.Empty;
    }

    private void ClearRecipeRows()
    {
        foreach (var row in _allRecipes)
        {
            row.PropertyChanged -= OnRecipeRowPropertyChanged;
        }

        _allRecipes.Clear();
        Recipes.Clear();
        AreAllRecipesChecked = false;
    }

    private void OnRecipeRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(SelectableItem<KdlRecipeDefinition>.IsChecked), StringComparison.Ordinal))
        {
            return;
        }

        UpdateCheckedSummaryState();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private void UpdateCheckedSummaryState()
    {
        _isUpdatingCheckedState = true;
        AreAllRecipesChecked = Recipes.Count > 0 && Recipes.All(item => item.IsChecked);
        _isUpdatingCheckedState = false;
    }

    private void SetLocalizedStatusMessage(Func<string> factory)
    {
        _statusMessageFactory = factory;
        StatusMessage = factory();
    }

    private void SetRawStatusMessage(string message)
    {
        _statusMessageFactory = null;
        StatusMessage = message;
    }

    private static bool TryParseLimit(string? value, out double result)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out result)
            || double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);
    }
}