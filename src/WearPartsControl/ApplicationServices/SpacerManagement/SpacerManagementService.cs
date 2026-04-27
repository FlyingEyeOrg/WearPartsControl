using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.UserConfig;
using WearPartsControl.Exceptions;
using UserConfigModel = WearPartsControl.ApplicationServices.UserConfig.UserConfig;

namespace WearPartsControl.ApplicationServices.SpacerManagement;

public sealed class SpacerManagementService : ISpacerManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILocalizationService _localizationService;
    private readonly IHttpRequestService _httpRequestService;
    private readonly IUserConfigService _userConfigService;
    private readonly ILogger<SpacerManagementService> _logger;

    public SpacerManagementService(
        ILocalizationService localizationService,
        IUserConfigService userConfigService,
        IHttpRequestService httpRequestService,
        ILogger<SpacerManagementService> logger)
    {
        _localizationService = localizationService;
        _userConfigService = userConfigService;
        _httpRequestService = httpRequestService;
        _logger = logger;
    }

    public async ValueTask<SpacerInfo> ParseCodeAsync(string code, string site, string resourceId, string cardId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new UserFriendlyException(L("SpacerManagement.CodeEmpty"));
        }

        if (string.IsNullOrWhiteSpace(site))
        {
            throw new UserFriendlyException(L("SpacerManagement.SiteEmpty"));
        }

        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new UserFriendlyException(L("SpacerManagement.ResourceIdEmpty"));
        }

        if (string.IsNullOrWhiteSpace(cardId))
        {
            throw new UserFriendlyException(L("SpacerManagement.CardIdEmpty"));
        }

        var userConfig = await _userConfigService.GetAsync(cancellationToken).ConfigureAwait(false);
        var separator = string.IsNullOrWhiteSpace(userConfig.SpacerValidationCodeSeparator)
            ? UserConfigModel.DefaultSpacerValidationCodeSeparator
            : userConfig.SpacerValidationCodeSeparator;
        var expectedCount = userConfig.SpacerValidationExpectedSegmentCount > 0
            ? userConfig.SpacerValidationExpectedSegmentCount
            : UserConfigModel.DefaultSpacerValidationExpectedSegmentCount;

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

        var userConfig = await _userConfigService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!userConfig.SpacerValidationEnabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(userConfig.SpacerValidationUrl))
        {
            throw new UserFriendlyException(L("SpacerManagement.ValidationUrlEmpty"));
        }

        if (userConfig.SpacerValidationTimeoutMilliseconds <= 0)
        {
            throw new UserFriendlyException(L("SpacerManagement.TimeoutInvalid"));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, userConfig.SpacerValidationUrl)
        {
            Content = JsonContent.Create(info)
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var response = await _httpRequestService.SendAsync(
                request,
                new HttpRequestExecutionOptions
                {
                    TimeoutMilliseconds = userConfig.SpacerValidationTimeoutMilliseconds,
                    IgnoreServerCertificateErrors = userConfig.SpacerValidationIgnoreServerCertificateErrors
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
            _logger.LogError(ex, "Spacer validation timeout");
            throw new UserFriendlyException(L("SpacerManagement.Timeout"));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Spacer validation HTTP request failed");
            throw new UserFriendlyException(string.Format(L("SpacerManagement.NetworkFailed"), ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Spacer validation unknown error");
            throw new UserFriendlyException(string.Format(L("SpacerManagement.Unknown"), ex.Message), null, null, ex);
        }
    }

    private string L(string key) => _localizationService[key];
}
