using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Exceptions;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class WearPartMonitoringHostedServiceTests
{
    [Fact]
    public async Task RunOnceAsync_WhenMonitorThrowsBusinessException_ShouldLogWarning()
    {
        var logger = new TestLogger<WearPartMonitoringHostedService>();
        var service = new WearPartMonitoringHostedService(
            new StubServiceScopeFactory(
                new StubAppSettingsService
                {
                    Current = new AppSettings { ResourceNumber = "RES-01" }
                },
                new StubWearPartMonitorService(new BusinessException("PLC 未连接")),
                new StubPlcStartupConnectionService()),
            logger);

        await service.RunOnceAsync();

        Assert.Contains(logger.Entries, entry => entry.LogLevel == LogLevel.Warning && entry.Message.Contains("PLC 未连接", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, entry => entry.LogLevel == LogLevel.Error);
    }

    [Fact]
    public async Task RunOnceAsync_WhenMonitoringDisabled_ShouldSkipMonitorCall()
    {
        var logger = new TestLogger<WearPartMonitoringHostedService>();
        var monitorService = new TrackableWearPartMonitorService();
        var service = new WearPartMonitoringHostedService(
            new StubServiceScopeFactory(
                new StubAppSettingsService
                {
                    Current = new AppSettings
                    {
                        ResourceNumber = "RES-01",
                        IsWearPartMonitoringEnabled = false
                    }
                },
                monitorService,
                new StubPlcStartupConnectionService()),
            logger);

        await service.RunOnceAsync();

        Assert.Equal(0, monitorService.CallCount);
        Assert.DoesNotContain(logger.Entries, entry => entry.LogLevel == LogLevel.Error);
    }

    [Fact]
    public async Task RunOnceAsync_WhenPlcConnectionFails_ShouldSkipMonitorCall()
    {
        var logger = new TestLogger<WearPartMonitoringHostedService>();
        var monitorService = new TrackableWearPartMonitorService();
        var service = new WearPartMonitoringHostedService(
            new StubServiceScopeFactory(
                new StubAppSettingsService
                {
                    Current = new AppSettings
                    {
                        ResourceNumber = "RES-01",
                        IsWearPartMonitoringEnabled = true
                    }
                },
                monitorService,
                new StubPlcStartupConnectionService
                {
                    Result = PlcStartupConnectionResult.Failed("PLC 连接失败")
                }),
            logger);

        await service.RunOnceAsync();

        Assert.Equal(0, monitorService.CallCount);
        Assert.Contains(logger.Entries, entry => entry.LogLevel == LogLevel.Warning && entry.Message.Contains("PLC 未连接", StringComparison.Ordinal));
    }

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public AppSettings Current { get; set; } = new();

        public event EventHandler<AppSettings>? SettingsSaved;

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Current);
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            SettingsSaved?.Invoke(this, settings);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubWearPartMonitorService : IWearPartMonitorService
    {
        private readonly Exception _exception;

        public StubWearPartMonitorService(Exception exception)
        {
            _exception = exception;
        }

        public Task<IReadOnlyList<WearPartMonitorResult>> MonitorByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromException<IReadOnlyList<WearPartMonitorResult>>(_exception);
        }

        public Task<IReadOnlyList<ExceedLimitRecord>> GetExceedLimitRecordsAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TrackableWearPartMonitorService : IWearPartMonitorService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<WearPartMonitorResult>> MonitorByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<WearPartMonitorResult>>([]);
        }

        public Task<IReadOnlyList<ExceedLimitRecord>> GetExceedLimitRecordsAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubServiceScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public StubServiceScopeFactory(
            IAppSettingsService appSettingsService,
            IWearPartMonitorService wearPartMonitorService,
            IPlcStartupConnectionService plcStartupConnectionService)
        {
            _serviceProvider = new StubServiceProvider(appSettingsService, wearPartMonitorService, plcStartupConnectionService);
        }

        public IServiceScope CreateScope() => new StubServiceScope(_serviceProvider);
    }

    private sealed class StubServiceScope : IServiceScope, IAsyncDisposable
    {
        public StubServiceScope(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services;

        public StubServiceProvider(
            IAppSettingsService appSettingsService,
            IWearPartMonitorService wearPartMonitorService,
            IPlcStartupConnectionService plcStartupConnectionService)
        {
            _services = new Dictionary<Type, object>
            {
                [typeof(IAppSettingsService)] = appSettingsService,
                [typeof(IWearPartMonitorService)] = wearPartMonitorService,
                [typeof(IPlcStartupConnectionService)] = plcStartupConnectionService
            };
        }

        public object? GetService(Type serviceType)
        {
            return _services.TryGetValue(serviceType, out var service)
                ? service
                : null;
        }
    }

    private sealed class StubPlcStartupConnectionService : IPlcStartupConnectionService
    {
        public PlcStartupConnectionResult Result { get; set; } = PlcStartupConnectionResult.Connected();

        public Task<PlcStartupConnectionResult> EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result);
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);
}