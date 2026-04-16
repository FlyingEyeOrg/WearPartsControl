using WearPartsControl.ApplicationServices.LoginService;

namespace WearPartsControl.ApplicationServices;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly object _syncRoot = new();
    private MhrUser? _currentUser;

    public event EventHandler? CurrentUserChanged;

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
        var changed = false;

        lock (_syncRoot)
        {
            var next = Clone(user);
            if (!AreEqual(_currentUser, next))
            {
                _currentUser = next;
                changed = true;
            }
        }

        if (changed)
        {
            CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear()
    {
        var changed = false;

        lock (_syncRoot)
        {
            if (_currentUser is not null)
            {
                _currentUser = null;
                changed = true;
            }
        }

        if (changed)
        {
            CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool AreEqual(MhrUser? left, MhrUser? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(left.CardId, right.CardId, StringComparison.Ordinal)
            && string.Equals(left.WorkId, right.WorkId, StringComparison.Ordinal)
            && left.AccessLevel == right.AccessLevel;
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