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

        try
        {
            var connecting = PlcStartupConnectionResult.Connecting(LocalizedText.Get("ViewModels.ClientAppInfoVm.TestingPlcConnection"));
            _plcConnectionStatusService.Set(connecting);

            var options = PlcConnectionOptionsFactory.Create(clientAppInfo);
            await _plcOperationPipeline.ConnectAsync(TestConnectOperationName, options, cancellationToken).ConfigureAwait(false);

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
}