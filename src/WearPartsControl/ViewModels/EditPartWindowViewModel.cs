using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class EditPartWindowViewModel : WearPartEditorViewModelBase
{
    public EditPartWindowViewModel(
        IWearPartManagementService wearPartManagementService,
        IUiBusyService uiBusyService)
        : base(wearPartManagementService, uiBusyService)
    {
    }

    protected override Task<WearPartDefinition> PersistAsync(WearPartDefinition definition, CancellationToken cancellationToken)
    {
        return WearPartManagementService.UpdateDefinitionAsync(definition, cancellationToken);
    }
}