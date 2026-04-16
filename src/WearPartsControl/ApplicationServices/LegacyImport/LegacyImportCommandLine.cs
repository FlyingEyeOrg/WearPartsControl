using System.IO;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.LegacyImport;

public static class LegacyImportCommandLine
{
    private const string ImportArgumentName = "--import-legacy-db";

    public static string? GetLegacyDatabasePathOrDefault(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 1 && LooksLikeDatabasePath(args[0]))
        {
            return Path.GetFullPath(args[0]);
        }

        for (var index = 0; index < args.Count; index++)
        {
            if (!string.Equals(args[index], ImportArgumentName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index == args.Count - 1 || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                throw new UserFriendlyException($"启动参数 {ImportArgumentName} 缺少旧版 SQLite 数据库文件路径。");
            }

            return Path.GetFullPath(args[index + 1]);
        }

        return null;
    }

    private static bool LooksLikeDatabasePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var extension = Path.GetExtension(value);
        return extension.Equals(".db", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sqlite", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sqlite3", StringComparison.OrdinalIgnoreCase);
    }
}