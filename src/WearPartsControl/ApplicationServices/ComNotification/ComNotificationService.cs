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
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.ComNotification;

public sealed class ComNotificationService : IComNotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISaveInfoStore _saveInfoStore;

    public ComNotificationService(ISaveInfoStore saveInfoStore)
    {
        _saveInfoStore = saveInfoStore;
    }

    public async ValueTask NotifyGroupAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
    {
        var options = await _saveInfoStore.ReadAsync<ComNotificationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
        if (!options.Enabled)
        {
            return;
        }

        ValidateBaseSettings(options);
        var users = ResolveUsers(options, toUsers);
        if (users.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.AccessToken) || string.IsNullOrWhiteSpace(options.Secret))
        {
            throw new UserFriendlyException("COM 群消息配置缺少 AccessToken 或 Secret");
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
                AccessToken = options.AccessToken,
                Secret = options.Secret
            }
        };

        await SendAsync(request, options, "群消息", cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask NotifyWorkAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, bool isAt = false, CancellationToken cancellationToken = default)
    {
        var options = await _saveInfoStore.ReadAsync<ComNotificationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
        if (!options.Enabled)
        {
            return;
        }

        ValidateBaseSettings(options);
        var users = ResolveUsers(options, toUsers);
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
            IsAt = isAt,
            TemplateData = new ComPushTemplateData
            {
                Title = title,
                Text = text
            }
        };

        await SendAsync(request, options, "工作消息", cancellationToken).ConfigureAwait(false);
    }

    private static List<string> ResolveUsers(ComNotificationOptionsSaveInfo options, IReadOnlyCollection<string>? toUsers)
    {
        if (toUsers is { Count: > 0 })
        {
            return toUsers
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .Select(static x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (string.IsNullOrWhiteSpace(options.DefaultUserWorkId))
        {
            return new List<string>();
        }

        return new List<string> { options.DefaultUserWorkId.Trim() };
    }

    private static void ValidateBaseSettings(ComNotificationOptionsSaveInfo options)
    {
        if (string.IsNullOrWhiteSpace(options.PushUrl))
        {
            throw new UserFriendlyException("COM 推送地址不能为空");
        }

        if (string.IsNullOrWhiteSpace(options.DeIpaasKeyAuth))
        {
            throw new UserFriendlyException("COM 推送缺少 deipaaskeyauth 配置");
        }

        if (string.IsNullOrWhiteSpace(options.UserType))
        {
            throw new UserFriendlyException("COM 推送缺少 UserType 配置");
        }

        if (options.TimeoutMilliseconds <= 0)
        {
            throw new UserFriendlyException("COM 推送 TimeoutMilliseconds 必须大于 0");
        }
    }

    private static async ValueTask SendAsync(ComPushRequest request, ComNotificationOptionsSaveInfo options, string scene, CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = static (_, _, _, _) => true
        };

        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds)
        };

        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Remove("deipaaskeyauth");
        httpClient.DefaultRequestHeaders.Add("deipaaskeyauth", options.DeIpaasKeyAuth);

        try
        {
            using var response = await httpClient.PostAsync(options.PushUrl, JsonContent.Create(request), cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            ComPushResponse? result = null;
            try
            {
                result = JsonSerializer.Deserialize<ComPushResponse>(content, JsonOptions);
            }
            catch (JsonException)
            {
                // handle below
            }

            if (result is null)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new UserFriendlyException($"COM {scene}发送失败: HTTP {(int)response.StatusCode}");
                }

                throw new UserFriendlyException($"COM {scene}发送失败: 响应内容无法解析");
            }

            if (!result.Success)
            {
                var error = result.Data?.ErrMessage;
                var message = string.IsNullOrWhiteSpace(error)
                    ? $"COM {scene}发送失败，code={result.Code}"
                    : $"COM {scene}发送失败，code={result.Code}，error={error}";

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
            throw new UserFriendlyException($"COM {scene}发送超时");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "COM {Scene}发送异常", scene);
            throw new UserFriendlyException($"COM {scene}发送失败: {ex.Message}");
        }
    }
}
