using WearPartsControl.ApplicationServices.Startup;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class AppSingleInstanceLeaseTests
{
    [Fact]
    public void TryAcquire_WhenInstanceAlreadyHeld_ShouldReturnFalse()
    {
        var instanceName = $"WearPartsControl.Tests.SingleInstance.{Guid.NewGuid():N}";

        Assert.True(AppSingleInstanceLease.TryAcquire(instanceName, out var firstLease));
        Assert.NotNull(firstLease);

        try
        {
            Assert.False(AppSingleInstanceLease.TryAcquire(instanceName, out var secondLease));
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
        var instanceName = $"WearPartsControl.Tests.SingleInstance.{Guid.NewGuid():N}";

        Assert.True(AppSingleInstanceLease.TryAcquire(instanceName, out var firstLease));
        Assert.NotNull(firstLease);
        firstLease.Dispose();

        Assert.True(AppSingleInstanceLease.TryAcquire(instanceName, out var secondLease));
        Assert.NotNull(secondLease);
        secondLease.Dispose();
    }

    [Fact]
    public void SignalActivation_WhenCallbackRegistered_ShouldInvokeCallback()
    {
        var instanceName = $"WearPartsControl.Tests.SingleInstance.{Guid.NewGuid():N}";
        using var activationRequested = new ManualResetEventSlim(false);

        Assert.True(AppSingleInstanceLease.TryAcquire(instanceName, out var lease));
        Assert.NotNull(lease);

        try
        {
            lease.RegisterActivationCallback(() => activationRequested.Set());

            AppSingleInstanceLease.SignalActivation(instanceName);

            Assert.True(activationRequested.Wait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            lease.Dispose();
        }
    }
}