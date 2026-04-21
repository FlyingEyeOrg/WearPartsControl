using System.Diagnostics;
using Serilog;

namespace WearPartsControl.ApplicationServices.Startup;

public static class ShutdownPerformanceTracker
{
    private static readonly object SyncRoot = new();
    private static Stopwatch? _stopwatch;
    private static long _lastMarkMilliseconds;

    public static void Restart(string stage)
    {
        lock (SyncRoot)
        {
            _stopwatch = Stopwatch.StartNew();
            _lastMarkMilliseconds = 0;
            Log.Information("关停阶段: {Stage}, +0ms, total 0ms", stage);
        }
    }

    public static void Mark(string stage)
    {
        lock (SyncRoot)
        {
            _stopwatch ??= Stopwatch.StartNew();
            var totalMilliseconds = _stopwatch.ElapsedMilliseconds;
            var deltaMilliseconds = totalMilliseconds - _lastMarkMilliseconds;
            _lastMarkMilliseconds = totalMilliseconds;
            Log.Information("关停阶段: {Stage}, +{DeltaMilliseconds}ms, total {TotalMilliseconds}ms", stage, deltaMilliseconds, totalMilliseconds);
        }
    }
}