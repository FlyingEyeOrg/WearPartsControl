using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class PartUpdateRecordViewModelTests
{
    [Fact]
    public async Task InitializeAsync_ShouldLoadRecordsAndPartDefinitions()
    {
        var viewModel = CreateViewModel(
            definitions:
            [
                new WearPartDefinition { Id = Guid.NewGuid(), PartName = "刀具A" },
                new WearPartDefinition { Id = Guid.NewGuid(), PartName = "刀具B" }
            ],
            records:
            [
                new WearPartReplacementRecord { WearPartDefinitionId = Guid.NewGuid(), PartName = "X", NewBarcode = "NB" },
                new WearPartReplacementRecord { WearPartDefinitionId = Guid.NewGuid(), PartName = "Y", NewBarcode = "NC" }
            ]);

        await viewModel.InitializeAsync();

        Assert.Equal(2, viewModel.Definitions.Count);
        Assert.Equal(2, viewModel.Records.Count);
        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(1, viewModel.TotalPages);
    }

    [Fact]
    public async Task QueryCommand_WhenDefinitionSelected_ShouldFilterRecords()
    {
        var definitionA = new WearPartDefinition { Id = Guid.NewGuid(), PartName = "刀具A" };
        var definitionB = new WearPartDefinition { Id = Guid.NewGuid(), PartName = "刀具B" };
        var viewModel = CreateViewModel(
            definitions: [definitionA, definitionB],
            records:
            [
                new WearPartReplacementRecord { WearPartDefinitionId = definitionA.Id, PartName = definitionA.PartName, NewBarcode = "A-1" },
                new WearPartReplacementRecord { WearPartDefinitionId = definitionB.Id, PartName = definitionB.PartName, NewBarcode = "B-1" }
            ]);

        await viewModel.InitializeAsync();
        viewModel.SelectedDefinition = definitionA;

        viewModel.QueryCommand.Execute(null);

        Assert.Single(viewModel.Records);
        Assert.Equal("A-1", viewModel.Records[0].NewBarcode);
    }

    [Fact]
    public async Task NextPageCommand_ShouldMoveToNextPage()
    {
        var definition = new WearPartDefinition { Id = Guid.NewGuid(), PartName = "刀具A" };
        var records = Enumerable.Range(1, 25)
            .Select(index => new WearPartReplacementRecord
            {
                WearPartDefinitionId = definition.Id,
                PartName = definition.PartName,
                NewBarcode = $"NB-{index}"
            })
            .ToArray();
        var viewModel = CreateViewModel([definition], records);

        await viewModel.InitializeAsync();
        viewModel.NextPageCommand.Execute(null);

        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Equal(5, viewModel.Records.Count);
    }

    [Fact]
    public async Task ExportCommand_ShouldRaiseExportRequestedWithCsvContent()
    {
        var definition = new WearPartDefinition { Id = Guid.NewGuid(), PartName = "刀具A" };
        var viewModel = CreateViewModel(
            [definition],
            [new WearPartReplacementRecord { WearPartDefinitionId = definition.Id, PartName = definition.PartName, NewBarcode = "NB-1", ReasonCode = WearPartReplacementReason.ProcessDamage, ReasonDisplayName = "过程损坏" }]);
        PartUpdateRecordExportRequestedEventArgs? raised = null;
        viewModel.ExportRequested += (_, args) => raised = args;

        await viewModel.InitializeAsync();
        viewModel.ExportCommand.Execute(null);

        Assert.NotNull(raised);
        Assert.Contains("名称,更换原因,当前编码,新编码", raised!.Content);
        Assert.Contains("刀具A", raised.Content);
        Assert.Contains("NB-1", raised.Content);
        Assert.Contains("过程损坏", raised.Content);
    }

    [Fact]
    public async Task SelectedPageSize_WhenChanged_ShouldUpdateCurrentPageDataAndTotalPages()
    {
        var definition = new WearPartDefinition { Id = Guid.NewGuid(), PartName = "刀具A" };
        var records = Enumerable.Range(1, 25)
            .Select(index => new WearPartReplacementRecord
            {
                WearPartDefinitionId = definition.Id,
                PartName = definition.PartName,
                NewBarcode = $"NB-{index}"
            })
            .ToArray();
        var viewModel = CreateViewModel([definition], records);

        await viewModel.InitializeAsync();

        viewModel.SelectedPageSize = 10;

        Assert.Equal(3, viewModel.TotalPages);
        Assert.Equal(10, viewModel.Records.Count);
        Assert.Equal(1, viewModel.CurrentPage);
    }

    [Fact]
    public void WearPartReplacementRecord_WhenOnlyReasonCodeProvided_ShouldResolveDisplayName()
    {
        var record = new WearPartReplacementRecord
        {
            ReasonCode = WearPartReplacementReason.ChangePosition
        };

        Assert.Equal(WearPartReplacementReason.ChangePosition, record.ReasonCode);
        Assert.Equal(WearPartReplacementReason.GetDisplayName(WearPartReplacementReason.ChangePosition), record.ReasonDisplayName);
    }

    [Fact]
    public async Task RefreshAsync_WhenNewReplacementRecordAdded_ShouldLoadLatestRecord()
    {
        var definition = new WearPartDefinition { Id = Guid.NewGuid(), PartName = "刀具A" };
        var replacementService = new StubWearPartReplacementService(
        [
            new WearPartReplacementRecord
            {
                Id = Guid.NewGuid(),
                WearPartDefinitionId = definition.Id,
                PartName = definition.PartName,
                NewBarcode = "NB-1",
                ReplacedAt = DateTime.UtcNow.AddMinutes(-1)
            }
        ]);
        var viewModel = new PartUpdateRecordViewModel(
            new StubClientAppInfoService(),
            new StubWearPartManagementService([definition]),
            replacementService,
            new UiBusyService(TimeSpan.Zero));

        await viewModel.InitializeAsync();

        replacementService.AddRecord(new WearPartReplacementRecord
        {
            Id = Guid.NewGuid(),
            WearPartDefinitionId = definition.Id,
            PartName = definition.PartName,
            NewBarcode = "NB-2",
            ReplacedAt = DateTime.UtcNow
        });

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(2, viewModel.Records.Count);
        Assert.Equal("NB-2", viewModel.Records[0].NewBarcode);
    }

    private static PartUpdateRecordViewModel CreateViewModel(
        IReadOnlyList<WearPartDefinition> definitions,
        IReadOnlyList<WearPartReplacementRecord> records)
    {
        return new PartUpdateRecordViewModel(
            new StubClientAppInfoService(),
            new StubWearPartManagementService(definitions),
            new StubWearPartReplacementService(records),
            new UiBusyService(TimeSpan.Zero));
    }

    private sealed class StubClientAppInfoService : IClientAppInfoService
    {
        private readonly Guid _id = Guid.NewGuid();

        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ClientAppInfoModel
            {
                Id = _id,
                ResourceNumber = "RES-01"
            });
        }

        public Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubWearPartManagementService : IWearPartManagementService
    {
        private readonly IReadOnlyList<WearPartDefinition> _definitions;

        public StubWearPartManagementService(IReadOnlyList<WearPartDefinition> definitions)
        {
            _definitions = definitions;
        }

        public Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_definitions);
        }

        public Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_definitions);
        }

        public Task<WearPartDefinition?> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<WearPartDefinition?>(null);
        public Task<WearPartDefinition> CreateDefinitionAsync(WearPartDefinition definition, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WearPartDefinition> UpdateDefinitionAsync(WearPartDefinition definition, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteDefinitionAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CopyDefinitionsAsync(string sourceResourceNumber, string targetResourceNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubWearPartReplacementService : IWearPartReplacementService
    {
        private readonly List<WearPartReplacementRecord> _records;

        public StubWearPartReplacementService(IReadOnlyList<WearPartReplacementRecord> records)
        {
            _records = records.ToList();
        }

        public Task<WearPartReplacementPreview> GetReplacementPreviewAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<WearPartReplacementRecord> ReplaceByScanAsync(WearPartReplacementRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<WearPartReplacementRecord>> GetReplacementHistoryAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WearPartReplacementRecord>>(_records.ToArray());
        }

        public void AddRecord(WearPartReplacementRecord record)
        {
            _records.Insert(0, record);
        }
    }
}