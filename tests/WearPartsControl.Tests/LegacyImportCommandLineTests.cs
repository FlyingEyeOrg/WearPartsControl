using WearPartsControl.ApplicationServices.LegacyImport;
using WearPartsControl.Exceptions;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(LocalizationSensitiveTestCollection.Name)]
public sealed class LegacyImportCommandLineTests
{
    [Fact]
    public void GetLegacyDatabasePathOrDefault_WhenArgumentValueMissing_ShouldThrowLocalizedChineseMessage()
    {
        using var cultureScope = new TestCultureScope("zh-CN");

        var exception = Assert.Throws<UserFriendlyException>(() => LegacyImportCommandLine.GetLegacyDatabasePathOrDefault(new[] { "--import-legacy-db" }));

        Assert.Equal("启动参数 --import-legacy-db 缺少旧版 SQLite 数据库文件路径。", exception.Message);
    }

    [Fact]
    public void GetLegacyDatabasePathOrDefault_WhenArgumentValueMissing_ShouldThrowLocalizedEnglishMessage()
    {
        using var cultureScope = new TestCultureScope("en-US");

        var exception = Assert.Throws<UserFriendlyException>(() => LegacyImportCommandLine.GetLegacyDatabasePathOrDefault(new[] { "--import-legacy-db" }));

        Assert.Equal("Startup argument --import-legacy-db is missing the legacy SQLite database file path.", exception.Message);
    }
}