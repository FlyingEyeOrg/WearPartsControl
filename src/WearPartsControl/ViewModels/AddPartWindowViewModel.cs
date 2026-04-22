using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class AddPartWindowViewModel : WearPartEditorViewModelBase
{
    public AddPartWindowViewModel(
        IWearPartManagementService wearPartManagementService,
        IToolChangeManagementService toolChangeManagementService,
        IUiBusyService uiBusyService)
        : base(wearPartManagementService, toolChangeManagementService, uiBusyService)
    {
    }

    protected override Task<WearPartDefinition> PersistAsync(WearPartDefinition definition, CancellationToken cancellationToken)
    {
        return WearPartManagementService.CreateDefinitionAsync(definition, cancellationToken);
    }
}