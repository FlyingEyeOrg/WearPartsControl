using WearPartsControl.Exceptions;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class BusinessExceptionTests
{
    [Fact]
    public void UserFriendlyException_ShouldInheritBusinessException()
    {
        var ex = new UserFriendlyException("Friendly");

        Assert.IsType<UserFriendlyException>(ex);
        Assert.IsAssignableFrom<BusinessException>(ex);
    }

    [Fact]
    public void BusinessException_WithData_ShouldStoreData()
    {
        var ex = new BusinessException("Biz failed", code: "App:0001").WithData("OrderId", 123);

        Assert.Equal("App:0001", ex.Code);
        Assert.Equal(123, ex.Data["OrderId"]);
    }
}
