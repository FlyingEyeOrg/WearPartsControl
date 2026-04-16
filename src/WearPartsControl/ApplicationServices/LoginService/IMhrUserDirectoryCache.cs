namespace WearPartsControl.ApplicationServices.LoginService;

public interface IMhrUserDirectoryCache
{
    Task<MhrUser?> FindUserAsync(
        string site,
        string resourceId,
        string authId,
        bool isIdCard,
        int cacheDays,
        CancellationToken cancellationToken = default);

    Task SaveUsersAsync(
        string site,
        string resourceId,
        IReadOnlyCollection<MhrUser> users,
        DateTime fetchedAt,
        CancellationToken cancellationToken = default);
}