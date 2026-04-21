using System.Net;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.SpacerManagement;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class SpacerManagementServiceTests
{
    [Fact]
    public async Task ParseCodeAsync_ShouldUseSavedSeparatorAndSegmentCount()
    {
        var store = new StubSaveInfoStore
        {
            Options = new SpacerValidationOptionsSaveInfo
            {
                CodeSeparator = "-",
                ExpectedSegmentCount = 8
            }
        };

        var service = new SpacerManagementService(new StubLocalizationService(), store, new StubHttpJsonService(), NullLogger<SpacerManagementService>.Instance);

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

    private sealed class StubSaveInfoStore : ISaveInfoStore
    {
        public SpacerValidationOptionsSaveInfo Options { get; set; } = new();

        public ValueTask<T> ReadAsync<T>(CancellationToken cancellationToken = default) where T : class, new()
        {
            if (typeof(T) == typeof(SpacerValidationOptionsSaveInfo))
            {
                return ValueTask.FromResult((T)(object)Options);
            }

            return ValueTask.FromResult(new T());
        }

        public ValueTask WriteAsync<T>(T model, CancellationToken cancellationToken = default) where T : class, new()
        {
            if (model is SpacerValidationOptionsSaveInfo options)
            {
                Options = options;
            }

            return ValueTask.CompletedTask;
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

    private sealed class StubHttpJsonService : IHttpJsonService
    {
        public ValueTask<TResponse> GetAsync<TResponse>(string requestUri, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<TResponse> PostAsync<TRequest, TResponse>(string requestUri, TRequest requestBody, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HttpRawResponse> SendRawAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options = null, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new HttpRawResponse((int)HttpStatusCode.OK, string.Empty, string.Empty));
        }
    }
}