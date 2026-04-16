using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Exceptions;
using WearPartsControl.Domain.Services;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class WearPartDefinitionDomainServiceTests
{
    private readonly WearPartDefinitionDomainService _domainService = new();

    [Fact]
    public void ValidateUniquePartNames_ShouldThrow_WhenDuplicateNamesExist()
    {
        var basicId = Guid.NewGuid();
        var definitions = new[]
        {
            new WearPartDefinitionEntity
            {
                Id = Guid.NewGuid(),
                ClientAppConfigurationId = basicId,
                ResourceNumber = "R001",
                PartName = "Knife",
                CurrentValueAddress = "DB1.0",
                WarningValueAddress = "DB1.1",
                ShutdownValueAddress = "DB1.2"
            },
            new WearPartDefinitionEntity
            {
                Id = Guid.NewGuid(),
                ClientAppConfigurationId = basicId,
                ResourceNumber = "R001",
                PartName = " knife ",
                CurrentValueAddress = "DB1.3",
                WarningValueAddress = "DB1.4",
                ShutdownValueAddress = "DB1.5"
            }
        };

        Assert.Throws<DomainBusinessException>(() => _domainService.ValidateUniquePartNames(definitions));
    }

    [Fact]
    public void ValidateEntity_ShouldThrow_WhenPartNameTooLong()
    {
        var entity = new WearPartDefinitionEntity
        {
            Id = Guid.NewGuid(),
            ClientAppConfigurationId = Guid.NewGuid(),
            ResourceNumber = "R001",
            PartName = new string('A', 129),
            CurrentValueAddress = "DB1.0",
            WarningValueAddress = "DB1.1",
            ShutdownValueAddress = "DB1.2"
        };

        Assert.Throws<DomainValidationException>(() => _domainService.ValidateEntity(entity));
    }
}
