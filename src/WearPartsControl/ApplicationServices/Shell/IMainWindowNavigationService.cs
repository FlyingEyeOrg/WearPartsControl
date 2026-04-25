namespace WearPartsControl.ApplicationServices.Shell;

public interface IMainWindowNavigationService
{
    IReadOnlyList<MainWindowTabItem> BuildVisibleTabs(IReadOnlyList<string> localizedTabHeaders, bool isClientAppInfoConfigured, string procedureCode);

    Type ResolveContentType(IReadOnlyList<MainWindowTabItem> visibleTabs, int index, bool isClientAppInfoConfigured, bool isLoggedIn);
}