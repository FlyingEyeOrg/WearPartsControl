using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class CurrentUserAccessorTests
{
    [Fact]
    public void SetCurrentUser_ShouldExposeWorkIdThroughICurrentUser()
    {
        var accessor = new CurrentUserAccessor();
        accessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-01",
            AccessLevel = 2
        });

        var currentUser = Assert.IsAssignableFrom<ICurrentUser>(accessor);
        Assert.Equal("WORK-01", currentUser.UserId);

        var snapshot = accessor.CurrentUser;
        Assert.NotNull(snapshot);
        Assert.Equal("CARD-01", snapshot!.CardId);
        Assert.Equal("WORK-01", snapshot.WorkId);
        Assert.Equal(2, snapshot.AccessLevel);
    }
}