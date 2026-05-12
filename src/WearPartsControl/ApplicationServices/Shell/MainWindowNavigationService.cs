using WearPartsControl.UserControls;

namespace WearPartsControl.ApplicationServices.Shell;

public sealed class MainWindowNavigationService : IMainWindowNavigationService
{
    private static readonly MainWindowTabKey[] ToolChangeProcedureTabs =
    [
        MainWindowTabKey.ReplacePart,
        MainWindowTabKey.ClientAppInfo,
        MainWindowTabKey.WearPartValuePreview,
        MainWindowTabKey.PartManagement,
        MainWindowTabKey.ToolChangeManagement,
        MainWindowTabKey.KdlRecipeManagement,
        MainWindowTabKey.PartReplacementHistory,
        MainWindowTabKey.WearPartMonitoringLog,
        MainWindowTabKey.UserConfig,
        MainWindowTabKey.ConfigurationTransfer
    ];

    private static readonly MainWindowTabKey[] StandardProcedureTabs =
    [
        MainWindowTabKey.ReplacePart,
        MainWindowTabKey.ClientAppInfo,
        MainWindowTabKey.WearPartValuePreview,
        MainWindowTabKey.PartManagement,
        MainWindowTabKey.PartReplacementHistory,
        MainWindowTabKey.WearPartMonitoringLog,
        MainWindowTabKey.UserConfig,
        MainWindowTabKey.ConfigurationTransfer
    ];

    private static readonly MainWindowTabKey[] UnconfiguredTabs =
    [
        MainWindowTabKey.ClientAppInfo,
        MainWindowTabKey.ConfigurationTransfer
    ];

    public IReadOnlyList<MainWindowTabItem> BuildVisibleTabs(IReadOnlyList<string> localizedTabHeaders, bool isClientAppInfoConfigured, string procedureCode)
    {
        ArgumentNullException.ThrowIfNull(localizedTabHeaders);

        if (localizedTabHeaders.Count < 10)
        {
            throw new InvalidOperationException("Main window tab headers are incomplete.");
        }

        if (!isClientAppInfoConfigured)
        {
            return UnconfiguredTabs
                .Select(key => CreateItem(key, localizedTabHeaders))
                .ToArray();
        }

        var visibleKeys = string.Equals(procedureCode, "模切分条", StringComparison.Ordinal)
            ? ToolChangeProcedureTabs
            : StandardProcedureTabs;

        return visibleKeys
            .Select(key => CreateItem(key, localizedTabHeaders))
            .ToArray();
    }

    public Type ResolveContentType(IReadOnlyList<MainWindowTabItem> visibleTabs, int index, bool isClientAppInfoConfigured, bool isLoggedIn)
    {
        ArgumentNullException.ThrowIfNull(visibleTabs);

        if (visibleTabs.Count == 0)
        {
            return typeof(ClientAppInfoUserControl);
        }

        if (index < 0 || index >= visibleTabs.Count)
        {
            index = 0;
        }

        var tab = visibleTabs[index].Key;
        if (!isClientAppInfoConfigured)
        {
            return tab == MainWindowTabKey.ConfigurationTransfer
                ? typeof(ConfigurationTransferUserControl)
                : typeof(ClientAppInfoUserControl);
        }

        if (!isLoggedIn && tab != MainWindowTabKey.ClientAppInfo)
        {
            return typeof(NeedLoginUserControl);
        }

        return tab switch
        {
            MainWindowTabKey.ReplacePart => typeof(ReplacePartUserControl),
            MainWindowTabKey.WearPartValuePreview => typeof(WearPartValuePreviewUserControl),
            MainWindowTabKey.ClientAppInfo => typeof(ClientAppInfoUserControl),
            MainWindowTabKey.PartManagement => typeof(PartManagementUserControl),
            MainWindowTabKey.ToolChangeManagement => typeof(ToolChangeManagementUserControl),
            MainWindowTabKey.KdlRecipeManagement => typeof(KdlRecipeManagementUserControl),
            MainWindowTabKey.PartReplacementHistory => typeof(PartReplacementHistoryUserControl),
            MainWindowTabKey.WearPartMonitoringLog => typeof(WearPartMonitoringLogUserControl),
            MainWindowTabKey.UserConfig => typeof(UserConfigUserControl),
            MainWindowTabKey.ConfigurationTransfer => typeof(ConfigurationTransferUserControl),
            _ => typeof(ReplacePartUserControl)
        };
    }

    private static MainWindowTabItem CreateItem(MainWindowTabKey key, IReadOnlyList<string> localizedTabHeaders)
    {
        return new MainWindowTabItem(key, localizedTabHeaders[(int)key]);
    }
}