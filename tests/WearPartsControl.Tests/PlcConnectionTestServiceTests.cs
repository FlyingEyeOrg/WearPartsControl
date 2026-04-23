using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PlcService;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class PlcConnectionTestServiceTests
{
    [Fact]
    public async Task TestAsync_WhenConnectionSucceeds_ShouldUpdateStatusAndCallPipeline()
    {
        var pipeline = new StubPlcOperationPipeline();
        var statusService = new PlcConnectionStatusService();
        var service = new PlcConnectionTestService(pipeline, statusService);

        var result = await service.TestAsync(new ClientAppInfoModel
        {
            SiteCode = "S01",
            FactoryCode = "F01",
            AreaCode = "阳极",
            ProcedureCode = "搅拌",
            EquipmentCode = "EQ-01",
            ResourceNumber = "RES-01",
            PlcProtocolType = "SiemensS1500",
            PlcIpAddress = "192.168.0.10",
            PlcPort = 102,
            ShutdownPointAddress = "M0.0",
            SiemensRack = 0,
            SiemensSlot = 0,
            IsStringReverse = false
        });

        Assert.Equal(PlcStartupConnectionStatus.Connected, result.Status);
        Assert.Equal(PlcStartupConnectionStatus.Connected, statusService.Current.Status);
        Assert.NotNull(pipeline.LastOptions);
        Assert.Equal("192.168.0.10", pipeline.LastOptions!.IpAddress);
    }

    [Fact]
    public async Task TestAsync_WhenBaseInfoMissing_ShouldReturnNotConfiguredAndSkipPipeline()
    {
        var pipeline = new StubPlcOperationPipeline();
        var statusService = new PlcConnectionStatusService();
        var service = new PlcConnectionTestService(pipeline, statusService);

        var result = await service.TestAsync(new ClientAppInfoModel());

        Assert.Equal(PlcStartupConnectionStatus.NotConfigured, result.Status);
        Assert.Equal(LocalizedText.Get("Services.ClientAppInfo.SiteCodeRequired"), result.Message);
        Assert.Equal(PlcStartupConnectionStatus.NotConfigured, statusService.Current.Status);
        Assert.Null(pipeline.LastOptions);
    }

    private sealed class StubPlcOperationPipeline : IPlcOperationPipeline
    {
        public PlcConnectionOptions? LastOptions { get; private set; }

        public Task ConnectAsync(string operationName, PlcConnectionOptions options, CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(string operationName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> IsConnectedAsync(string operationName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TValue> ReadAsync<TValue>(string operationName, string address, int retryCount = 1, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task WriteAsync<TValue>(string operationName, string address, TValue value, int retryCount = 1, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}