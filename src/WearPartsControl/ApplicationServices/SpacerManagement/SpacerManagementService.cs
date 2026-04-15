using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.SpacerManagement;

public sealed class SpacerManagementService : ISpacerManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILocalizationService _localizationService;
    private readonly IHttpJsonService _httpJsonService;
    private readonly ISaveInfoStore _saveInfoStore;

    public SpacerManagementService(ILocalizationService localizationService, ISaveInfoStore saveInfoStore, IHttpJsonService httpJsonService)
    {
        _localizationService = localizationService;
        _saveInfoStore = saveInfoStore;
        _httpJsonService = httpJsonService;
    }

    public SpacerInfo ParseCode(string code, string site, string resourceId, string cardId)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new UserFriendlyException(L("SpacerManagement.CodeEmpty"));
        }

        var options = _saveInfoStore.ReadAsync<SpacerValidationOptionsSaveInfo>().AsTask().GetAwaiter().GetResult();
        var separator = string.IsNullOrWhiteSpace(options.CodeSeparator) ? "/" : options.CodeSeparator;
        var expectedCount = options.ExpectedSegmentCount > 0 ? options.ExpectedSegmentCount : 8;

        var parts = code.Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != expectedCount)
        {
            throw new UserFriendlyException(string.Format(L("SpacerManagement.CodeSegmentCountInvalid"), expectedCount, parts.Length));
        }

        return new SpacerInfo
        {
            Site = site,
            ResourceId = resourceId,
            Operator = cardId,
            ModelPn = parts[0],
            Date = parts[1],
            BigCoatingWidth = parts[2],
            SmallCoatingWidth = parts[3],
            WhiteSpaceWidth = parts[4],
            AT11Width = parts[5],
            Thickness = parts[6],
            ABSite = parts[7]
        };
    }

    public async ValueTask VerifyAsync(SpacerInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);

        var options = await _saveInfoStore.ReadAsync<SpacerValidationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
        if (!options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ValidationUrl))
        {
            throw new UserFriendlyException(L("SpacerManagement.ValidationUrlEmpty"));
        }

        if (options.TimeoutMilliseconds <= 0)
        {
            throw new UserFriendlyException(L("SpacerManagement.TimeoutInvalid"));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, options.ValidationUrl)
        {
            Content = JsonContent.Create(info)
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var response = await _httpJsonService.SendRawAsync(
                request,
                new HttpRequestExecutionOptions
                {
                    TimeoutMilliseconds = options.TimeoutMilliseconds,
                    IgnoreServerCertificateErrors = options.IgnoreServerCertificateErrors
                },
                cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return;
            }

            SpacerApiErrorResponse? apiError = null;
            try
            {
                apiError = JsonSerializer.Deserialize<SpacerApiErrorResponse>(response.Body, JsonOptions);
            }
            catch (JsonException)
            {
                // ignore and fallback to generic message
            }

            if (apiError?.Error is not null)
            {
                throw new UserFriendlyException(
                        apiError.Error.Message ?? L("SpacerManagement.ValidationFailed"),
                    apiError.Error.Code,
                    apiError.Error.Details);
            }

                    throw new UserFriendlyException(string.Format(L("SpacerManagement.HttpFailed"), response.StatusCode));
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Error(ex, "Spacer validation timeout");
            throw new UserFriendlyException(L("SpacerManagement.Timeout"));
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Spacer validation HTTP request failed");
            throw new UserFriendlyException(string.Format(L("SpacerManagement.NetworkFailed"), ex.Message));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Spacer validation unknown error");
            throw new UserFriendlyException(string.Format(L("SpacerManagement.Unknown"), ex.Message), null, null, ex);
        }
    }

    private string L(string key) => _localizationService[key];
}
