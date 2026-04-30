using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace WearPartsControl.ApplicationServices.MonitoringLogs;

public sealed class WearPartMonitoringLogPipeline : IWearPartMonitoringLogPipeline, IDisposable
{
    private const int DefaultCapacity = 2000;
    private const int DefaultDispatchQueueCapacity = 1024;
    private const int MaxDispatchBatchSize = 100;

    private readonly object _syncRoot = new();
    private readonly Queue<WearPartMonitoringLogEntry> _entries = new();
    private readonly Channel<WearPartMonitoringLogPipelineNotification> _notificationChannel;
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private readonly Task _notificationTask;
    private int _isDisposed;
    private long _sequence;

    public WearPartMonitoringLogPipeline()
        : this(DefaultCapacity, DefaultDispatchQueueCapacity)
    {
    }

    internal WearPartMonitoringLogPipeline(int capacity, int dispatchQueueCapacity = DefaultDispatchQueueCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (dispatchQueueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dispatchQueueCapacity));
        }

        Capacity = capacity;
        _notificationChannel = Channel.CreateBounded<WearPartMonitoringLogPipelineNotification>(new BoundedChannelOptions(dispatchQueueCapacity)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _notificationTask = Task.Run(ProcessNotificationsAsync);
    }

    public event EventHandler<WearPartMonitoringLogEntriesAddedEventArgs>? EntriesAdded;

    public event EventHandler? Cleared;

    public int Capacity { get; }

    public int RetainedCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _entries.Count;
            }
        }
    }

    public IReadOnlyList<WearPartMonitoringLogEntry> Snapshot()
    {
        lock (_syncRoot)
        {
            return _entries.ToArray();
        }
    }

    public WearPartMonitoringLogPage Query(WearPartMonitoringLogQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Offset));
        }

        if (query.Limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Limit));
        }

        WearPartMonitoringLogEntry[] snapshot;
        lock (_syncRoot)
        {
            snapshot = _entries.ToArray();
        }

        var pageEntries = new List<WearPartMonitoringLogEntry>(Math.Min(query.Limit, snapshot.Length));
        var totalCount = 0;
        for (var index = snapshot.Length - 1; index >= 0; index--)
        {
            var entry = snapshot[index];
            if (!Matches(entry, query))
            {
                continue;
            }

            if (totalCount >= query.Offset && pageEntries.Count < query.Limit)
            {
                pageEntries.Add(entry);
            }

            totalCount++;
        }

        pageEntries.Reverse();
        return new WearPartMonitoringLogPage(pageEntries, totalCount, Math.Min(query.Offset, totalCount), query.Limit, snapshot.Length);
    }

    public void Publish(
        WearPartMonitoringLogLevel level,
        WearPartMonitoringLogCategory category,
        string message,
        string? operationName = null,
        string? resourceNumber = null,
        string? address = null,
        string? details = null,
        Exception? exception = null)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            return;
        }

        var entry = new WearPartMonitoringLogEntry(
            Interlocked.Increment(ref _sequence),
            DateTimeOffset.Now,
            level,
            category,
            Normalize(message),
            NormalizeOptional(operationName),
            NormalizeOptional(resourceNumber),
            NormalizeOptional(address),
            BuildDetails(details, exception));

        lock (_syncRoot)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > Capacity)
            {
                _entries.Dequeue();
            }
        }

        _notificationChannel.Writer.TryWrite(WearPartMonitoringLogPipelineNotification.ForEntry(entry));
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _entries.Clear();
        }

        _notificationChannel.Writer.TryWrite(WearPartMonitoringLogPipelineNotification.Cleared);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _notificationChannel.Writer.TryComplete();
        _disposeTokenSource.Cancel();

        try
        {
            _notificationTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(exception => exception is OperationCanceledException))
        {
        }
        finally
        {
            _disposeTokenSource.Dispose();
        }
    }

    private async Task ProcessNotificationsAsync()
    {
        var token = _disposeTokenSource.Token;
        var batch = new List<WearPartMonitoringLogEntry>(MaxDispatchBatchSize);

        try
        {
            while (await _notificationChannel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (_notificationChannel.Reader.TryRead(out var notification))
                {
                    if (notification.IsClear)
                    {
                        RaiseEntriesAdded(batch);
                        batch.Clear();
                        RaiseCleared();
                        continue;
                    }

                    if (notification.Entry is null)
                    {
                        continue;
                    }

                    batch.Add(notification.Entry);
                    if (batch.Count >= MaxDispatchBatchSize)
                    {
                        RaiseEntriesAdded(batch);
                        batch.Clear();
                    }
                }

                RaiseEntriesAdded(batch);
                batch.Clear();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private void RaiseEntriesAdded(IReadOnlyList<WearPartMonitoringLogEntry> entries)
    {
        if (entries.Count == 0 || EntriesAdded is not { } handlers)
        {
            return;
        }

        var args = new WearPartMonitoringLogEntriesAddedEventArgs(entries.ToArray());
        foreach (EventHandler<WearPartMonitoringLogEntriesAddedEventArgs> handler in handlers.GetInvocationList().Cast<EventHandler<WearPartMonitoringLogEntriesAddedEventArgs>>())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }

    private void RaiseCleared()
    {
        if (Cleared is not { } handlers)
        {
            return;
        }

        foreach (EventHandler handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? BuildDetails(string? details, Exception? exception)
    {
        if (exception is null)
        {
            return NormalizeOptional(details);
        }

        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(details))
        {
            builder.AppendLine(details.Trim());
        }

        builder.Append(exception);
        return builder.ToString();
    }

    private static bool Matches(WearPartMonitoringLogEntry entry, WearPartMonitoringLogQuery query)
    {
        if (query.Level is { } level && entry.Level != level)
        {
            return false;
        }

        if (query.Category is { } category && entry.Category != category)
        {
            return false;
        }

        var keyword = NormalizeOptional(query.Keyword);
        if (keyword is null)
        {
            return true;
        }

        return Contains(entry.Message, keyword)
            || Contains(entry.Details, keyword)
            || Contains(entry.OperationName, keyword)
            || Contains(entry.ResourceNumber, keyword)
            || Contains(entry.Address, keyword);
    }

    private static bool Contains(string? value, string keyword)
    {
        return value?.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }

    private sealed record WearPartMonitoringLogPipelineNotification(WearPartMonitoringLogEntry? Entry, bool IsClear)
    {
        public static WearPartMonitoringLogPipelineNotification Cleared { get; } = new(null, true);

        public static WearPartMonitoringLogPipelineNotification ForEntry(WearPartMonitoringLogEntry entry)
        {
            return new WearPartMonitoringLogPipelineNotification(entry, false);
        }
    }
}