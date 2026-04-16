namespace WearPartsControl.ApplicationServices.LegacyImport;

public sealed class LegacyDatabaseImportResult
{
    public string LegacyDatabasePath { get; set; } = string.Empty;

    public int ImportedClientConfigurations { get; set; }

    public int UpdatedClientConfigurations { get; set; }

    public int ImportedWearPartDefinitions { get; set; }

    public int UpdatedWearPartDefinitions { get; set; }

    public int ImportedReplacementRecords { get; set; }

    public int ImportedExceedLimitRecords { get; set; }

    public int SkippedRows { get; set; }

    public string ToSummary()
    {
        return $"旧库导入完成。\n数据库: {LegacyDatabasePath}\n客户端配置: 新增 {ImportedClientConfigurations}，更新 {UpdatedClientConfigurations}\n易损件定义: 新增 {ImportedWearPartDefinitions}，更新 {UpdatedWearPartDefinitions}\n更换记录: 新增 {ImportedReplacementRecords}\n超限记录: 新增 {ImportedExceedLimitRecords}\n跳过行数: {SkippedRows}";
    }
}