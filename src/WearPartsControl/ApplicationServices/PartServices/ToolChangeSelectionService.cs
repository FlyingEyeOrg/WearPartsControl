using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class ToolChangeSelectionService : IToolChangeSelectionService
{
    private const int MaxRecentToolCodes = 20;

    private readonly ISaveInfoStore _saveInfoStore;

    public ToolChangeSelectionService(ISaveInfoStore saveInfoStore)
    {
        _saveInfoStore = saveInfoStore;
    }

    public async ValueTask<ToolChangeSelectionState> GetStateAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
    {
        var saveInfo = await _saveInfoStore.ReadAsync<ToolChangeSelectionSaveInfo>(cancellationToken).ConfigureAwait(false);
        var selectedToolCode = saveInfo.Items
            .FirstOrDefault(x => x.WearPartDefinitionId == wearPartDefinitionId)
            ?.SelectedToolCode ?? string.Empty;

        return new ToolChangeSelectionState
        {
            SelectedToolCode = selectedToolCode,
            RecentToolCodes = saveInfo.RecentToolCodes.ToArray()
        };
    }

    public async ValueTask SaveSelectionAsync(Guid wearPartDefinitionId, string toolCode, CancellationToken cancellationToken = default)
    {
        if (wearPartDefinitionId == Guid.Empty)
        {
            return;
        }

        var normalizedToolCode = Normalize(toolCode);
        var saveInfo = await _saveInfoStore.ReadAsync<ToolChangeSelectionSaveInfo>(cancellationToken).ConfigureAwait(false);
        var item = saveInfo.Items.FirstOrDefault(x => x.WearPartDefinitionId == wearPartDefinitionId);

        if (string.IsNullOrWhiteSpace(normalizedToolCode))
        {
            if (item is not null)
            {
                saveInfo.Items.Remove(item);
            }

            await _saveInfoStore.WriteAsync(saveInfo, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (item is null)
        {
            saveInfo.Items.Add(new ToolChangeSelectionItem
            {
                WearPartDefinitionId = wearPartDefinitionId,
                SelectedToolCode = normalizedToolCode
            });
        }
        else
        {
            item.SelectedToolCode = normalizedToolCode;
        }

        saveInfo.RecentToolCodes.RemoveAll(x => string.Equals(x, normalizedToolCode, StringComparison.OrdinalIgnoreCase));
        saveInfo.RecentToolCodes.Insert(0, normalizedToolCode);
        if (saveInfo.RecentToolCodes.Count > MaxRecentToolCodes)
        {
            saveInfo.RecentToolCodes.RemoveRange(MaxRecentToolCodes, saveInfo.RecentToolCodes.Count - MaxRecentToolCodes);
        }

        await _saveInfoStore.WriteAsync(saveInfo, cancellationToken).ConfigureAwait(false);
    }

    private static string Normalize(string? toolCode)
    {
        return toolCode?.Trim() ?? string.Empty;
    }
}

[SaveInfoFile("tool-change-selection")]
public sealed class ToolChangeSelectionSaveInfo
{
    public List<ToolChangeSelectionItem> Items { get; set; } = [];

    public List<string> RecentToolCodes { get; set; } = [];
}

public sealed class ToolChangeSelectionItem
{
    public Guid WearPartDefinitionId { get; set; }

    public string SelectedToolCode { get; set; } = string.Empty;
}