using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class ToolChangeManagementViewModelTests
{
    [Fact]
    public async Task SaveCommand_WhenCreatingDefinition_ShouldRefreshDefinitions()
    {
        var service = new StubToolChangeManagementService();
        var viewModel = new ToolChangeManagementViewModel(service, new UiBusyService(TimeSpan.Zero));

        await viewModel.InitializeAsync();
        viewModel.ToolName = "标准刀";
        viewModel.ToolCode = "TL-01";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Definitions);
        Assert.Equal("标准刀", viewModel.Definitions[0].Name);
        Assert.Equal("TL-01", viewModel.Definitions[0].Code);
        Assert.Same(viewModel.Definitions[0], viewModel.SelectedDefinition);
        Assert.DoesNotContain("正在新建", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(1, service.CreateCount);
    }

    private sealed class StubToolChangeManagementService : IToolChangeManagementService
    {
        private readonly List<ToolChangeDefinition> _definitions = [];

        public int CreateCount { get; private set; }

        public Task<IReadOnlyList<ToolChangeDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ToolChangeDefinition>>(_definitions.OrderBy(x => x.Name).ToArray());
        }

        public Task<ToolChangeDefinition> CreateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default)
        {
            CreateCount++;
            var created = new ToolChangeDefinition
            {
                Id = Guid.NewGuid(),
                Name = definition.Name,
                Code = definition.Code
            };
            _definitions.Add(created);
            return Task.FromResult(created);
        }

        public Task<ToolChangeDefinition> UpdateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}