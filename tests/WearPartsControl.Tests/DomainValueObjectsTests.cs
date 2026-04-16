using WearPartsControl.Domain.Exceptions;
using WearPartsControl.Domain.ValueObjects;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class DomainValueObjectsTests
{
    [Fact]
    public void ResourceNumber_ShouldTrimValue()
    {
        var resourceNumber = new ResourceNumber("  R001  ");

        Assert.Equal("R001", resourceNumber.Value);
    }

    [Fact]
    public void ResourceNumber_ShouldThrow_WhenEmpty()
    {
        Assert.Throws<DomainValidationException>(() => new ResourceNumber("   "));
    }

    [Fact]
    public void PlcEndpoint_ShouldThrow_WhenPortOutOfRange()
    {
        Assert.Throws<DomainValidationException>(() => new PlcEndpoint("127.0.0.1", 0));
    }
}
