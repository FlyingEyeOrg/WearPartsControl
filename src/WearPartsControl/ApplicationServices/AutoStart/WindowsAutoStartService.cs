using Microsoft.Win32;
using System.IO;

namespace WearPartsControl.ApplicationServices.AutoStart;

public sealed class WindowsAutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WearPartsControl";

    public ValueTask<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = runKey?.GetValue(RunValueName) as string;
        return ValueTask.FromResult(!string.IsNullOrWhiteSpace(value));
    }

    public ValueTask SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            runKey.SetValue(RunValueName, QuoteExecutablePath(ResolveExecutablePath()), RegistryValueKind.String);
        }
        else
        {
            runKey.DeleteValue(RunValueName, throwOnMissingValue: false);
        }

        return ValueTask.CompletedTask;
    }

    private static string ResolveExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "WearPartsControl.exe");
    }

    private static string QuoteExecutablePath(string path)
    {
        return $"\"{path}\"";
    }
}