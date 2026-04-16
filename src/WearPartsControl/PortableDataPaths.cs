using System.IO;

namespace WearPartsControl;

public static class PortableDataPaths
{
    public const string RootDirectoryName = "PrivateData";

    public static string RootDirectory => EnsureDirectory(Path.Combine(AppContext.BaseDirectory, RootDirectoryName));

    public static string SettingsDirectory => EnsureDirectory(Path.Combine(RootDirectory, "Settings"));

    public static string DatabaseDirectory => EnsureDirectory(Path.Combine(RootDirectory, "LocalDB"));

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
