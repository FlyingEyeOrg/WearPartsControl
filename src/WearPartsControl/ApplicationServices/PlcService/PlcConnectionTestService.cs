using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcConnectionTestService : IPlcConnectionTestService
{
    private const string TestConnectOperationName = "ClientAppInfo/TestConnect";

    private readonly IPlcOperationPipeline _plcOperationPipeline;
    private readonly IPlcConnectionStatusService _plcConnectionStatusService;

    public PlcConnectionTestService(IPlcOperationPipeline plcOperationPipeline, IPlcConnectionStatusService plcConnectionStatusService)
    {
        _plcOperationPipeline = plcOperationPipeline;
        _plcConnectionStatusService = plcConnectionStatusService;
    }

    public async Task<PlcStartupConnectionResult> TestAsync(ClientAppInfoModel clientAppInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientAppInfo);

        var validationMessage = ValidateClientAppInfo(clientAppInfo);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            var notConfigured = PlcStartupConnectionResult.NotConfigured(validationMessage);
            _plcConnectionStatusService.Set(notConfigured);
            return notConfigured;
        }

        try
        {
            var connecting = PlcStartupConnectionResult.Connecting(LocalizedText.Get("ViewModels.ClientAppInfoVm.TestingPlcConnection"));
            _plcConnectionStatusService.Set(connecting);

            var options = PlcConnectionOptionsFactory.Create(clientAppInfo);
            await _plcOperationPipeline.ForceReconnectAsync(TestConnectOperationName, options, cancellationToken).ConfigureAwait(false);

            var connected = PlcStartupConnectionResult.Connected(LocalizedText.Get("ViewModels.ClientAppInfoVm.PlcConnectionTestSucceeded"));
            _plcConnectionStatusService.Set(connected);
            return connected;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failed = PlcStartupConnectionResult.Failed(LocalizedText.Format("Services.PlcStartupConnection.ConnectFailed", ex.Message));
            _plcConnectionStatusService.Set(failed);
            return failed;
        }
    }

    private static string? ValidateClientAppInfo(ClientAppInfoModel clientAppInfo)
    {
        return GetRequiredMessage(clientAppInfo.SiteCode, "Services.ClientAppInfo.SiteCodeRequired")
            ?? GetRequiredMessage(clientAppInfo.FactoryCode, "Services.ClientAppInfo.FactoryCodeRequired")
            ?? GetRequiredMessage(clientAppInfo.AreaCode, "Services.ClientAppInfo.AreaCodeRequired")
            ?? GetRequiredMessage(clientAppInfo.ProcedureCode, "Services.ClientAppInfo.ProcedureCodeRequired")
            ?? GetRequiredMessage(clientAppInfo.EquipmentCode, "Services.ClientAppInfo.EquipmentCodeRequired")
            ?? GetRequiredMessage(clientAppInfo.ResourceNumber, "Services.ClientAppInfo.ResourceNumberRequired")
            ?? GetRequiredMessage(clientAppInfo.PlcProtocolType, "Services.ClientAppInfo.PlcProtocolRequired")
            ?? GetRequiredMessage(clientAppInfo.PlcIpAddress, "Services.ClientAppInfo.PlcIpRequired")
            ?? GetRequiredMessage(clientAppInfo.ShutdownPointAddress, "Services.ClientAppInfo.ShutdownPointAddressRequired");
    }

    private static string? GetRequiredMessage(string? value, string localizationKey)
    {
        return string.IsNullOrWhiteSpace(value)
            ? LocalizedText.Get(localizationKey)
            : null;
    }
}