using System.Net;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SpacerManagement;
using WearPartsControl.ApplicationServices.UserConfig;
using WearPartsControl.Exceptions;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class SpacerManagementServiceTests
{
    [Fact]
    public async Task ParseCodeAsync_ShouldUseSavedSeparatorAndSegmentCount()
    {
        var store = new StubSaveInfoStore
        {
            Config = new UserConfig
            {
                SpacerValidationCodeSeparator = "-",
                SpacerValidationExpectedSegmentCount = 8
            }
        };

        var service = new SpacerManagementService(new StubLocalizationService(), store, new StubHttpRequestService(), NullLogger<SpacerManagementService>.Instance);

        var info = await service.ParseCodeAsync("PN-20260421-0.20-0.10-0.05-AT11-1.50-AB", "SITE01", "RES01", "CARD01");

        Assert.Equal("SITE01", info.Site);
        Assert.Equal("RES01", info.ResourceId);
        Assert.Equal("CARD01", info.Operator);
        Assert.Equal("PN", info.ModelPn);
        Assert.Equal("20260421", info.Date);
        Assert.Equal("1.50", info.Thickness);
        Assert.Equal("0.20", info.BigCoatingWidth);
        Assert.Equal("0.10", info.SmallCoatingWidth);
        Assert.Equal("0.05", info.WhiteSpaceWidth);
        Assert.Equal("AT11", info.AT11Width);
        Assert.Equal("AB", info.ABSite);
    }

    [Fact]
    public async Task ParseCodeAsync_WhenResourceIdEmpty_ShouldThrowBeforeReadingConfig()
    {
        var service = new SpacerManagementService(new StubLocalizationService(), new ThrowingUserConfigService(), new StubHttpRequestService(), NullLogger<SpacerManagementService>.Instance);

        var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => service.ParseCodeAsync("PN-20260421-0.20-0.10-0.05-AT11-1.50-AB", "SITE01", " ", "CARD01").AsTask());

        Assert.Equal("SpacerManagement.ResourceIdEmpty", exception.Message);
    }

    private sealed class StubSaveInfoStore : IUserConfigService
    {
        public UserConfig Config { get; set; } = new();

        public ValueTask<UserConfig> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Config);
        }

        public ValueTask SaveAsync(UserConfig config, CancellationToken cancellationToken = default)
        {
            Config = config;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingUserConfigService : IUserConfigService
    {
        public ValueTask<UserConfig> GetAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Configuration should not be read when parse input is invalid.");
        }

        public ValueTask SaveAsync(UserConfig config, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        public string this[string name] => name;

        public ApplicationServices.Localization.Generated.LocalizationCatalog Catalog { get; } = new(static key => key);

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public System.Globalization.CultureInfo CurrentCulture { get; } = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
    }

    private sealed class StubHttpRequestService : IHttpRequestService
    {
        public ValueTask<HttpRawResponse> SendAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options = null, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new HttpRawResponse((int)HttpStatusCode.OK, string.Empty, string.Empty));
        }
    }
}