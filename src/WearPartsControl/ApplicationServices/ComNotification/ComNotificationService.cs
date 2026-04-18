using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;
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
    private readonly IHttpJsonService _httpJsonService;
    private readonly ISaveInfoStore _saveInfoStore;
    private readonly IUserConfigService _userConfigService;

    public ComNotificationService(ISaveInfoStore saveInfoStore, ILocalizationService localizationService, IHttpJsonService httpJsonService, IUserConfigService userConfigService)
    {
        _saveInfoStore = saveInfoStore;
        _localizationService = localizationService;
        _httpJsonService = httpJsonService;
        _userConfigService = userConfigService;
    }

    public async ValueTask NotifyGroupAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
    {
        var options = await _saveInfoStore.ReadAsync<ComNotificationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
        var userConfig = await _userConfigService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!options.Enabled)
        {
            return;
        }

        ValidateBaseSettings(options);
        var users = ResolveUsers(options, userConfig, toUsers);
        if (users.Count == 0)
        {
            return;
        }

        var accessToken = ResolveAccessToken(options, userConfig);
        var secret = ResolveSecret(options, userConfig);
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(secret))
        {
            throw new UserFriendlyException(L("ComNotification.GroupTokenMissing"));
        }

        var request = new ComPushRequest
        {
            AgentId = options.AgentId,
            TemplateId = options.GroupTemplateId,
            ToAll = false,
            ToUser = users,
            UserType = options.UserType,
            TemplateData = new ComPushTemplateData
            {
                Title = title,
                Text = text,
                AccessToken = accessToken,
                Secret = secret
            }
        };

        await SendAsync(request, options, "群消息", cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask NotifyWorkAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
    {
        var options = await _saveInfoStore.ReadAsync<ComNotificationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
        var userConfig = await _userConfigService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!options.Enabled)
        {
            return;
        }

        ValidateBaseSettings(options);
        var users = ResolveUsers(options, userConfig, toUsers);
        if (users.Count == 0)
        {
            return;
        }

        var request = new ComPushRequest
        {
            AgentId = options.AgentId,
            TemplateId = options.WorkTemplateId,
            ToAll = false,
            ToUser = users,
            UserType = options.UserType,
            IsAt = false,
            TemplateData = new ComPushTemplateData
            {
                Title = title,
                Text = text
            }
        };

        await SendAsync(request, options, "工作消息", cancellationToken).ConfigureAwait(false);
    }

    private static List<string> ResolveUsers(ComNotificationOptionsSaveInfo options, UserConfigModel userConfig, IReadOnlyCollection<string>? toUsers)
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

        if (string.IsNullOrWhiteSpace(options.DefaultUserWorkId))
        {
            return new List<string>();
        }

        return new List<string> { options.DefaultUserWorkId.Trim() };
    }

    private static string ResolveAccessToken(ComNotificationOptionsSaveInfo options, UserConfigModel userConfig)
        => string.IsNullOrWhiteSpace(userConfig.ComAccessToken) ? options.AccessToken : userConfig.ComAccessToken;

    private static string ResolveSecret(ComNotificationOptionsSaveInfo options, UserConfigModel userConfig)
        => string.IsNullOrWhiteSpace(userConfig.ComSecret) ? options.Secret : userConfig.ComSecret;

    private void ValidateBaseSettings(ComNotificationOptionsSaveInfo options)
    {
        if (string.IsNullOrWhiteSpace(options.PushUrl))
        {
            throw new UserFriendlyException(L("ComNotification.PushUrlEmpty"));
        }

        if (string.IsNullOrWhiteSpace(options.DeIpaasKeyAuth))
        {
            throw new UserFriendlyException(L("ComNotification.AuthKeyMissing"));
        }

        if (string.IsNullOrWhiteSpace(options.UserType))
        {
            throw new UserFriendlyException(L("ComNotification.UserTypeMissing"));
        }

        if (options.TimeoutMilliseconds <= 0)
        {
            throw new UserFriendlyException(L("ComNotification.TimeoutInvalid"));
        }
    }

    private async ValueTask SendAsync(ComPushRequest request, ComNotificationOptionsSaveInfo options, string scene, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, options.PushUrl)
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Headers.Remove("deipaaskeyauth");
        httpRequest.Headers.Add("deipaaskeyauth", options.DeIpaasKeyAuth);

        try
        {
            var response = await _httpJsonService.SendRawAsync(
                httpRequest,
                new HttpRequestExecutionOptions
                {
                    TimeoutMilliseconds = options.TimeoutMilliseconds,
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
            Log.Error(ex, "COM {Scene}发送超时", scene);
            throw new UserFriendlyException(string.Format(L("ComNotification.Timeout"), scene));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "COM {Scene}发送异常", scene);
            throw new UserFriendlyException(string.Format(L("ComNotification.Unhandled"), scene, ex.Message));
        }
    }

    private string L(string key) => _localizationService[key];
}
