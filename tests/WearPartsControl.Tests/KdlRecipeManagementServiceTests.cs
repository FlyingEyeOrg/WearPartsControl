using System.IO;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.Exceptions;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class KdlRecipeManagementServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldPersistRecipeAndSetFirstRecipeCurrent()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), $"WearPartsControl.KdlRecipe.{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var accessor = CreateCurrentUserAccessor();
            var service = new KdlRecipeManagementService(accessor, new TypeJsonSaveInfoStore(settingsDirectory));

            var created = await service.CreateAsync(new KdlRecipeDefinition
            {
                Name = "PF-01",
                LowerLimit = 0.12,
                UpperLimit = 0.34
            });

            var state = await service.GetAsync();

            var current = Assert.Single(state.Recipes);
            Assert.True(created.IsCurrent);
            Assert.Equal(created.Id, state.CurrentRecipeId);
            Assert.True(current.IsCurrent);
            Assert.Equal("PF-01", current.Name);
            Assert.Equal(0.12, current.LowerLimit);
            Assert.Equal(0.34, current.UpperLimit);
            Assert.Equal("WORK-KDL", current.CreatedBy);
            Assert.True(File.Exists(Path.Combine(settingsDirectory, "kdl-recipe-settings.json")));
        }
        finally
        {
            if (Directory.Exists(settingsDirectory))
            {
                Directory.Delete(settingsDirectory, true);
            }
        }
    }

    [Fact]
    public async Task DeleteAsync_WhenCurrentRecipeDeleted_ShouldPromoteRemainingRecipe()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), $"WearPartsControl.KdlRecipe.{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var accessor = CreateCurrentUserAccessor();
            var service = new KdlRecipeManagementService(accessor, new TypeJsonSaveInfoStore(settingsDirectory));
            var first = await service.CreateAsync(new KdlRecipeDefinition
            {
                Name = "PF-01",
                LowerLimit = 0.10,
                UpperLimit = 0.20
            });
            var second = await service.CreateAsync(new KdlRecipeDefinition
            {
                Name = "PF-02",
                LowerLimit = 0.21,
                UpperLimit = 0.30
            });

            await service.DeleteAsync(first.Id);

            var current = await service.GetCurrentRecipeAsync();
            Assert.NotNull(current);
            Assert.Equal(second.Id, current!.Id);
            Assert.True(current.IsCurrent);
        }
        finally
        {
            if (Directory.Exists(settingsDirectory))
            {
                Directory.Delete(settingsDirectory, true);
            }
        }
    }

    [Fact]
    public async Task CreateAsync_WhenLowerLimitExceedsUpperLimit_ShouldThrowUserFriendlyException()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), $"WearPartsControl.KdlRecipe.{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var service = new KdlRecipeManagementService(CreateCurrentUserAccessor(), new TypeJsonSaveInfoStore(settingsDirectory));

            var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => service.CreateAsync(new KdlRecipeDefinition
            {
                Name = "PF-01",
                LowerLimit = 0.50,
                UpperLimit = 0.10
            }).AsTask());

            Assert.Equal(LocalizedText.Format("Services.KdlRecipeManagement.RangeInvalid", "0.5", "0.1"), exception.Message);
        }
        finally
        {
            if (Directory.Exists(settingsDirectory))
            {
                Directory.Delete(settingsDirectory, true);
            }
        }
    }

    private static CurrentUserAccessor CreateCurrentUserAccessor()
    {
        var accessor = new CurrentUserAccessor();
        accessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-KDL",
            WorkId = "WORK-KDL",
            AccessLevel = 3
        });
        return accessor;
    }
}