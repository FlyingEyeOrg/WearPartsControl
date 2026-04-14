using System;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.ApplicationServices.SaveInfoService;

public static class SaveInfo
{
    private static ISaveInfoStore _store = new TypeJsonSaveInfoStore();

    public static void SetStore(ISaveInfoStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static ValueTask<T> ReadAsync<T>(CancellationToken cancellationToken = default) where T : class, new()
    {
        return _store.ReadAsync<T>(cancellationToken);
    }

    public static ValueTask WriteAsync<T>(T model, CancellationToken cancellationToken = default) where T : class, new()
    {
        return _store.WriteAsync(model, cancellationToken);
    }
}
