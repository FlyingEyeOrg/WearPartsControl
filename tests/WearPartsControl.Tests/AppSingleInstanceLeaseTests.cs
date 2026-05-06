using WearPartsControl.ApplicationServices.Startup;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class AppSingleInstanceLeaseTests
{
    [Fact]
    public void TryAcquire_WhenMutexAlreadyHeld_ShouldReturnFalse()
    {
        var mutexName = $@"Local\WearPartsControl.Tests.SingleInstance.{Guid.NewGuid():N}";

        Assert.True(AppSingleInstanceLease.TryAcquire(mutexName, out var firstLease));
        Assert.NotNull(firstLease);

        try
        {
            Assert.False(AppSingleInstanceLease.TryAcquire(mutexName, out var secondLease));
            Assert.Null(secondLease);
        }
        finally
        {
            firstLease.Dispose();
        }
    }

    [Fact]
    public void TryAcquire_WhenPreviousLeaseDisposed_ShouldAllowReacquire()
    {
        var mutexName = $@"Local\WearPartsControl.Tests.SingleInstance.{Guid.NewGuid():N}";

        Assert.True(AppSingleInstanceLease.TryAcquire(mutexName, out var firstLease));
        Assert.NotNull(firstLease);
        firstLease.Dispose();

        Assert.True(AppSingleInstanceLease.TryAcquire(mutexName, out var secondLease));
        Assert.NotNull(secondLease);
        secondLease.Dispose();
    }
}