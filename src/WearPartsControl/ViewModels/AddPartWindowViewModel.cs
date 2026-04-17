using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class AddPartWindowViewModel : WearPartEditorViewModelBase
{
    public AddPartWindowViewModel(
        IWearPartManagementService wearPartManagementService,
        IUiBusyService uiBusyService)
        : base(wearPartManagementService, uiBusyService)
    {
    }

    protected override Task<WearPartDefinition> PersistAsync(WearPartDefinition definition, CancellationToken cancellationToken)
    {
        return WearPartManagementService.CreateDefinitionAsync(definition, cancellationToken);
    }
}