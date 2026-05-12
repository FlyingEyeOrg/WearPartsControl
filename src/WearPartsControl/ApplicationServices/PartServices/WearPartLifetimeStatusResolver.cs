namespace WearPartsControl.ApplicationServices.PartServices;

internal static class WearPartLifetimeStatusResolver
{
    public static WearPartMonitorStatus Resolve(double currentValue, double warningValue, double shutdownValue)
    {
        if (shutdownValue > 0d && currentValue >= shutdownValue)
        {
            return WearPartMonitorStatus.Shutdown;
        }

        if (warningValue > 0d && currentValue >= warningValue)
        {
            return WearPartMonitorStatus.Warning;
        }

        return WearPartMonitorStatus.Normal;
    }
}