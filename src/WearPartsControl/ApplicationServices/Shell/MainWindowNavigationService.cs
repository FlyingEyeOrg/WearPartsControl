using WearPartsControl.UserControls;

namespace WearPartsControl.ApplicationServices.Shell;

public sealed class MainWindowNavigationService : IMainWindowNavigationService
{
    private static readonly MainWindowTabKey[] ToolChangeProcedureTabs =
    [
        MainWindowTabKey.ReplacePart,
        MainWindowTabKey.ClientAppInfo,
        MainWindowTabKey.PartManagement,
        MainWindowTabKey.ToolChangeManagement,
        MainWindowTabKey.PartReplacementHistory,
        MainWindowTabKey.UserConfig
    ];

    private static readonly MainWindowTabKey[] StandardProcedureTabs =
    [
        MainWindowTabKey.ReplacePart,
        MainWindowTabKey.ClientAppInfo,
        MainWindowTabKey.PartManagement,
        MainWindowTabKey.PartReplacementHistory,
        MainWindowTabKey.UserConfig
    ];

    public IReadOnlyList<MainWindowTabItem> BuildVisibleTabs(IReadOnlyList<string> localizedTabHeaders, bool isClientAppInfoConfigured, string procedureCode)
    {
        ArgumentNullException.ThrowIfNull(localizedTabHeaders);

        if (localizedTabHeaders.Count < 6)
        {
            throw new InvalidOperationException("Main window tab headers are incomplete.");
        }

        if (!isClientAppInfoConfigured)
        {
            return [CreateItem(MainWindowTabKey.ClientAppInfo, localizedTabHeaders)];
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

        if (!isClientAppInfoConfigured || visibleTabs.Count == 0)
        {
            return typeof(ClientAppInfoUserControl);
        }

        if (index < 0 || index >= visibleTabs.Count)
        {
            index = 0;
        }

        var tab = visibleTabs[index].Key;
        if (!isLoggedIn && tab != MainWindowTabKey.ClientAppInfo)
        {
            return typeof(NeedLoginUserControl);
        }

        return tab switch
        {
            MainWindowTabKey.ReplacePart => typeof(ReplacePartUserControl),
            MainWindowTabKey.ClientAppInfo => typeof(ClientAppInfoUserControl),
            MainWindowTabKey.PartManagement => typeof(PartManagementUserControl),
            MainWindowTabKey.ToolChangeManagement => typeof(ToolChangeManagementUserControl),
            MainWindowTabKey.PartReplacementHistory => typeof(PartReplacementHistoryUserControl),
            MainWindowTabKey.UserConfig => typeof(UserConfigUserControl),
            _ => typeof(ReplacePartUserControl)
        };
    }

    private static MainWindowTabItem CreateItem(MainWindowTabKey key, IReadOnlyList<string> localizedTabHeaders)
    {
        return new MainWindowTabItem(key, localizedTabHeaders[(int)key]);
    }
}