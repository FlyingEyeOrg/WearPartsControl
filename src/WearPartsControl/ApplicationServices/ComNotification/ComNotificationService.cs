using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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

namespace WearPartsControl.ApplicationServices.ComNotification;

public sealed class ComNotificationService : IComNotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILocalizationService _localizationService;
    private readonly IHttpRequestService _httpRequestService;
    private readonly IUserConfigService _userConfigService;
    private readonly ILogger<ComNotificationService> _logger;

    public ComNotificationService(
        ILocalizationService localizationService,
        IHttpRequestService httpRequestService,
        IUserConfigService userConfigService,
        ILogger<ComNotificationService> logger)
    {
        _localizationService = localizationService;
        _httpRequestService = httpRequestService;
        _userConfigService = userConfigService;
        _logger = logger;
    }

    public async ValueTask NotifyGroupAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
    {
        var userConfig = await _userConfigService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!userConfig.ComNotificationEnabled)
        {
            return;
        }

        ValidateBaseSettings(userConfig);
        var users = ResolveUsers(userConfig, toUsers);
        if (users.Count == 0)
        {
            return;
        }

        var accessToken = ResolveAccessToken(userConfig);
        var secret = ResolveSecret(userConfig);
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(secret))
        {
            throw new UserFriendlyException(L("ComNotification.GroupTokenMissing"));
        }

        var request = new ComPushRequest
        {
            AgentId = userConfig.ComAgentId,
            TemplateId = userConfig.ComGroupTemplateId,
            ToAll = false,
            ToUser = users,
            UserType = userConfig.ComUserType,
            TemplateData = new ComPushTemplateData
            {
                Title = title,
                Text = text,
                AccessToken = accessToken,
                Secret = secret
            }
        };

        await SendAsync(request, userConfig, L("ComNotification.GroupMessageScene"), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask NotifyWorkAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
    {
        var userConfig = await _userConfigService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!userConfig.ComNotificationEnabled)
        {
            return;
        }

        ValidateBaseSettings(userConfig);
        var users = ResolveUsers(userConfig, toUsers);
        if (users.Count == 0)
        {
            return;
        }

        var request = new ComPushRequest
        {
            AgentId = userConfig.ComAgentId,
            TemplateId = userConfig.ComWorkTemplateId,
            ToAll = false,
            ToUser = users,
            UserType = userConfig.ComUserType,
            IsAt = false,
            TemplateData = new ComPushTemplateData
            {
                Title = title,
                Text = text
            }
        };

        await SendAsync(request, userConfig, L("ComNotification.WorkMessageScene"), cancellationToken).ConfigureAwait(false);
    }

    private static List<string> ResolveUsers(UserConfigModel userConfig, IReadOnlyCollection<string>? toUsers)
    {
        if (toUsers is { Count: > 0 })
        {
            return toUsers
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .Select(static x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var userConfiguredRecipients = new[]
        {
            userConfig.MeResponsibleWorkId,
            userConfig.PrdResponsibleWorkId
        }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (userConfiguredRecipients.Count > 0)
        {
            return userConfiguredRecipients;
        }

        return new List<string>();
    }

    private static string ResolveAccessToken(UserConfigModel userConfig)
        => userConfig.ComAccessToken;

    private static string ResolveSecret(UserConfigModel userConfig)
        => userConfig.ComSecret;

    private void ValidateBaseSettings(UserConfigModel userConfig)
    {
        if (string.IsNullOrWhiteSpace(userConfig.ComPushUrl))
        {
            throw new UserFriendlyException(L("ComNotification.PushUrlEmpty"));
        }

        if (string.IsNullOrWhiteSpace(userConfig.ComDeIpaasKeyAuth))
        {
            throw new UserFriendlyException(L("ComNotification.AuthKeyMissing"));
        }

        if (string.IsNullOrWhiteSpace(userConfig.ComUserType))
        {
            throw new UserFriendlyException(L("ComNotification.UserTypeMissing"));
        }

        if (userConfig.ComTimeoutMilliseconds <= 0)
        {
            throw new UserFriendlyException(L("ComNotification.TimeoutInvalid"));
        }
    }

    private async ValueTask SendAsync(ComPushRequest request, UserConfigModel userConfig, string scene, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, userConfig.ComPushUrl)
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Headers.Remove("deipaaskeyauth");
        httpRequest.Headers.Add("deipaaskeyauth", userConfig.ComDeIpaasKeyAuth);

        try
        {
            var response = await _httpRequestService.SendAsync(
                httpRequest,
                new HttpRequestExecutionOptions
                {
                    TimeoutMilliseconds = userConfig.ComTimeoutMilliseconds,
                    IgnoreServerCertificateErrors = true
                },
                cancellationToken).ConfigureAwait(false);

            ComPushResponse? result = null;
            try
            {
                result = JsonSerializer.Deserialize<ComPushResponse>(response.Body, JsonOptions);
            }
            catch (JsonException)
            {
                // handle below
            }

            if (result is null)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new UserFriendlyException(string.Format(L("ComNotification.HttpFailed"), scene, response.StatusCode));
                }

                throw new UserFriendlyException(string.Format(L("ComNotification.ResponseParseFailed"), scene));
            }

            if (!result.Success)
            {
                var error = result.Data?.ErrMessage;
                var message = string.IsNullOrWhiteSpace(error)
                    ? string.Format(L("ComNotification.PushFailedWithCode"), scene, result.Code)
                    : string.Format(L("ComNotification.PushFailedWithError"), scene, result.Code, error);

                throw new UserFriendlyException(message);
            }
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "COM {Scene}发送超时", scene);
            throw new UserFriendlyException(string.Format(L("ComNotification.Timeout"), scene));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "COM {Scene}发送异常", scene);
            throw new UserFriendlyException(string.Format(L("ComNotification.Unhandled"), scene, ex.Message));
        }
    }

    private string L(string key) => _localizationService[key];
}
