using System.Globalization;
using System.Net.Http;
using System.Threading;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.Localization.Generated;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.Exceptions;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class CutterMesValidationServiceTests
{
    [Fact]
    public async Task GetExpectedCutterCodeAsync_ShouldUseSharedHttpServiceAndParseExpectedCode()
    {
        var httpRequestService = new StubHttpRequestService
        {
            Response = new HttpRawResponse(200, "OK", """
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
  <soapenv:Body>
    <ns2:getParametricValueResponse xmlns:ns2="http://machineintegration.ws.atlmes.com/">
      <return>
        <code>0</code>
        <message>success</message>
        <parametricDataArray>
          <parameter>QDBH-UP</parameter>
          <value>CUTTER-001</value>
        </parametricDataArray>
      </return>
    </ns2:getParametricValueResponse>
  </soapenv:Body>
</soapenv:Envelope>
"""
)
        };
        var service = new CutterMesValidationService(httpRequestService);

        var result = await service.GetExpectedCutterCodeAsync(new CutterMesValidationRequest
        {
            Wsdl = "https://mes.example.com/service?wsdl",
            UserName = "mes-user",
            Password = "mes-pass",
            Site = "MES-S01",
            RollNumber = "ROLL-001",
            Parameter = "QDBH-UP"
        });

        Assert.Equal("CUTTER-001", result);
        Assert.Equal("https://mes.example.com/service", httpRequestService.LastRequestUri);
        Assert.Equal("Basic bWVzLXVzZXI6bWVzLXBhc3M=", httpRequestService.LastAuthorizationHeader);
        Assert.NotNull(httpRequestService.LastRequestBody);
        Assert.Contains("<site>MES-S01</site>", httpRequestService.LastRequestBody!);
        Assert.Contains("<sfc>ROLL-001</sfc>", httpRequestService.LastRequestBody!);
        Assert.Contains("<parameter>QDBH-UP</parameter>", httpRequestService.LastRequestBody!);
    }

    [Fact]
    public async Task GetExpectedCutterCodeAsync_WhenHttpFailed_ShouldWrapException()
    {
        var httpRequestService = new StubHttpRequestService
        {
            Response = new HttpRawResponse(500, "Server Error", "failure")
        };
        var service = new CutterMesValidationService(httpRequestService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetExpectedCutterCodeAsync(new CutterMesValidationRequest
        {
            Wsdl = "https://mes.example.com/service?wsdl",
            UserName = "mes-user",
            Password = "mes-pass",
            Site = "MES-S01",
            RollNumber = "ROLL-001",
            Parameter = "QDBH-UP"
        }));

        Assert.Contains("调用离线校验接口错误", exception.Message);
    }

  [Fact]
  public async Task GetExpectedCutterCodeAsync_WhenMesConfigurationMissing_ShouldThrowUserFriendlyExceptionBeforeHttpCall()
  {
    var httpRequestService = new StubHttpRequestService();
    var service = new CutterMesValidationService(httpRequestService);

    var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => service.GetExpectedCutterCodeAsync(new CutterMesValidationRequest
    {
      Wsdl = " ",
      UserName = "mes-user",
      Password = "mes-pass",
      Site = "MES-S01",
      RollNumber = "ROLL-001",
      Parameter = "QDBH-UP"
    }));

    Assert.Equal(LocalizedText.Get("Services.WearPartReplacement.CutterMesConfigurationMissing"), exception.Message);
    Assert.Null(httpRequestService.LastRequestUri);
  }

    private sealed class StubHttpRequestService : IHttpRequestService
    {
        public HttpRawResponse Response { get; set; } = new(200, "OK", string.Empty);

        public string? LastRequestUri { get; private set; }

        public string? LastAuthorizationHeader { get; private set; }

        public string? LastRequestBody { get; private set; }

        public async ValueTask<HttpRawResponse> SendAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastRequestUri = request.RequestUri?.ToString();
            LastAuthorizationHeader = request.Headers.Authorization?.ToString();
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return Response;
        }
    }
}