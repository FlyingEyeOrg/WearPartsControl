using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class EditPartWindowViewModel : WearPartEditorViewModelBase
{
    public EditPartWindowViewModel(
        IWearPartManagementService wearPartManagementService,
        IWearPartTypeService wearPartTypeService,
        IUiBusyService uiBusyService)
        : base(wearPartManagementService, wearPartTypeService, uiBusyService)
    {
    }

    protected override Task<WearPartDefinition> PersistAsync(WearPartDefinition definition, CancellationToken cancellationToken)
    {
        return WearPartManagementService.UpdateDefinitionAsync(definition, cancellationToken);
    }
}