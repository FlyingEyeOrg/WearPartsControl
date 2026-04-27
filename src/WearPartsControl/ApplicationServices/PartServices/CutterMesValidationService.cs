using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Xml.Linq;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class CutterMesValidationService : ICutterMesValidationService
{
    private static readonly XNamespace SoapEnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace AtlMesNamespace = "http://machineintegration.ws.atlmes.com/";
    private readonly IHttpRequestService _httpRequestService;

    public CutterMesValidationService(IHttpRequestService httpRequestService)
    {
        _httpRequestService = httpRequestService;
    }

    public async Task<string> GetExpectedCutterCodeAsync(CutterMesValidationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var endpoint = ResolveServiceEndpoint(request.Wsdl);
        var payload = BuildEnvelope(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "text/xml")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{request.UserName}:{request.Password}")));

        try
        {
            var response = await _httpRequestService.SendAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
            EnsureSuccessStatusCode(response);
            return ParseExpectedCutterCode(response.Body, request.Parameter);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(LocalizedText.Format("Services.WearPartReplacement.CutterMesCallFailed", ex.Message), ex);
        }
    }

    private static string BuildEnvelope(CutterMesValidationRequest request)
    {
        return $"""
<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:atl=\"http://machineintegration.ws.atlmes.com/\">
  <soapenv:Header />
  <soapenv:Body>
    <atl:getParametricValue>
      <GetParametricValueRequest>
        <site>{Escape(request.Site)}</site>
        <sfc>{Escape(request.RollNumber)}</sfc>
        <parametricDataArray>
          <parameter>{Escape(request.Parameter)}</parameter>
        </parametricDataArray>
        <parametricDataArray>
          <parameter>DXDT</parameter>
        </parametricDataArray>
        <parametricDataArray>
          <parameter>DZYT</parameter>
        </parametricDataArray>
        <parametricDataArray>
          <parameter>KDL</parameter>
        </parametricDataArray>
      </GetParametricValueRequest>
    </atl:getParametricValue>
  </soapenv:Body>
</soapenv:Envelope>
""";
    }

    private static void ValidateRequest(CutterMesValidationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Wsdl)
            || string.IsNullOrWhiteSpace(request.UserName)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.Site))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.CutterMesConfigurationMissing"));
        }

        if (string.IsNullOrWhiteSpace(request.RollNumber))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.CutterRollNumberInvalid"));
        }

        if (string.IsNullOrWhiteSpace(request.Parameter))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.CutterMesPositionUnresolved"));
        }
    }

    private static string ParseExpectedCutterCode(string xml, string requestedParameter)
    {
        var document = XDocument.Parse(xml);
        var body = document.Descendants(SoapEnvelopeNamespace + "Body").FirstOrDefault()
            ?? throw new InvalidOperationException(LocalizedText.Get("Services.WearPartReplacement.CutterMesResponseInvalid"));
        var returnElement = body.Descendants().FirstOrDefault(x => x.Name.LocalName == "return")
            ?? throw new InvalidOperationException(LocalizedText.Get("Services.WearPartReplacement.CutterMesResponseInvalid"));

        var codeText = returnElement.Elements().FirstOrDefault(x => x.Name.LocalName == "code")?.Value;
        if (!int.TryParse(codeText, out var code))
        {
            throw new InvalidOperationException(LocalizedText.Get("Services.WearPartReplacement.CutterMesResponseInvalid"));
        }

        var message = returnElement.Elements().FirstOrDefault(x => x.Name.LocalName == "message")?.Value ?? string.Empty;
        if (code != 0)
        {
            throw new InvalidOperationException($"{LocalizedText.Get("Services.WearPartReplacement.CutterMesBusinessFailed")} {message}".Trim());
        }

        var expectedCode = returnElement.Elements()
            .Where(x => x.Name.LocalName == "parametricDataArray")
            .Select(x => new
            {
                Parameter = x.Elements().FirstOrDefault(child => child.Name.LocalName == "parameter")?.Value ?? string.Empty,
                Value = x.Elements().FirstOrDefault(child => child.Name.LocalName == "value")?.Value ?? string.Empty
            })
            .FirstOrDefault(x => string.Equals(x.Parameter, requestedParameter, StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim();

        if (string.IsNullOrWhiteSpace(expectedCode))
        {
            throw new InvalidOperationException(LocalizedText.Get("Services.WearPartReplacement.CutterMesCutterCodeMissing"));
        }

        return expectedCode;
    }

    private static void EnsureSuccessStatusCode(HttpRawResponse response)
    {
        if (response.StatusCode is >= 200 and <= 299)
        {
            return;
        }

        throw new HttpRequestException($"Response status code does not indicate success: {response.StatusCode} ({response.ReasonPhrase ?? string.Empty}).".Trim());
    }

    private static string ResolveServiceEndpoint(string wsdl)
    {
        var normalized = wsdl?.Trim() ?? string.Empty;
        if (normalized.EndsWith("?wsdl", StringComparison.OrdinalIgnoreCase))
        {
            return normalized[..^5];
        }

        return normalized;
    }

    private static string Escape(string? value)
    {
        return SecurityElement.Escape(value?.Trim() ?? string.Empty) ?? string.Empty;
    }
}