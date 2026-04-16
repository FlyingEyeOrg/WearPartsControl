using WearPartsControl.ApplicationServices.LoginService;

namespace WearPartsControl.ApplicationServices;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly object _syncRoot = new();
    private MhrUser? _currentUser;

    public MhrUser? CurrentUser
    {
        get
        {
            lock (_syncRoot)
            {
                return Clone(_currentUser);
            }
        }
    }

    public string? UserId
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentUser?.WorkId;
            }
        }
    }

    public void SetCurrentUser(MhrUser? user)
    {
        lock (_syncRoot)
        {
            _currentUser = Clone(user);
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _currentUser = null;
        }
    }

    private static MhrUser? Clone(MhrUser? user)
    {
        if (user is null)
        {
            return null;
        }

        return new MhrUser
        {
            CardId = user.CardId,
            WorkId = user.WorkId,
            AccessLevel = user.AccessLevel
        };
    }
}