using WearPartsControl.ApplicationServices.LegacyImport;
using WearPartsControl.ApplicationServices.PartServices;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class LegacyImportValueConverterTests
{
    [Theory]
    [InlineData("Real", "FLOAT")]
    [InlineData("LReal", "DOUBLE")]
    [InlineData("Int", "INT16")]
    [InlineData("DInt", "INT32")]
    [InlineData("UDInt", "UINT32")]
    [InlineData("Bool", "BOOL")]
    public void NormalizeWearPartDataType_ShouldMapLegacyPlcTypesToPipelineTypes(string legacyType, string expectedType)
    {
        var normalized = LegacyImportValueConverter.NormalizeWearPartDataType(legacyType);

        Assert.Equal(expectedType, normalized);
    }

    [Theory]
    [InlineData("0", "JSON")]
    [InlineData("1", "STRING")]
    [InlineData("2", "INT32")]
    [InlineData("3", "FLOAT")]
    [InlineData("4", "DOUBLE")]
    [InlineData("Int", "INT32")]
    public void NormalizeReplacementDataType_ShouldMapLegacyEnumStorage(string legacyType, string expectedType)
    {
        var normalized = LegacyImportValueConverter.NormalizeReplacementDataType(legacyType);

        Assert.Equal(expectedType, normalized);
    }

    [Theory]
    [InlineData("西门子S1500", "SiemensS1500")]
    [InlineData("西门子S1200", "SiemensS1200")]
    [InlineData("欧姆龙CIP", "OmronCip")]
    [InlineData("欧姆龙Fins", "OmronFins")]
    [InlineData("三菱", "Mitsubishi")]
    [InlineData("罗克韦尔", "AllenBradley")]
    [InlineData("汇川AM", "InovanceAm")]
    [InlineData("汇川H3U", "InovanceH3U")]
    [InlineData("汇川H5U", "InovanceH5U")]
    [InlineData("汇川EIP", "InovanceEip")]
    [InlineData("倍福", "Beckhoff")]
    [InlineData("基恩士", "Keyence")]
    [InlineData("ModbusTcp", "ModbusTcp")]
    [InlineData("S7", "SiemensS1500")]
    public void NormalizePlcProtocolType_ShouldMapLegacyProtocolNames(string legacyType, string expectedType)
    {
        var normalized = LegacyImportValueConverter.NormalizePlcProtocolType(legacyType);

        Assert.Equal(expectedType, normalized);
    }

    [Theory]
    [InlineData("键盘", "Manual")]
    [InlineData("扫码枪", "Scanner")]
    [InlineData("Barcode", "Scanner")]
    public void NormalizeInputMode_ShouldMapLegacyInputModes(string legacyValue, string expectedValue)
    {
        var normalized = LegacyImportValueConverter.NormalizeInputMode(legacyValue);

        Assert.Equal(expectedValue, normalized);
    }

    [Theory]
    [InlineData("计米", "记米")]
    [InlineData("计次", "计次")]
    [InlineData("时间", "计时")]
    [InlineData("Meter", "记米")]
    [InlineData("Time", "计时")]
    public void NormalizeLifetimeType_ShouldMapLegacyLifetimeTypes(string legacyValue, string expectedValue)
    {
        var normalized = LegacyImportValueConverter.NormalizeLifetimeType(legacyValue);

        Assert.Equal(expectedValue, normalized);
    }

    [Theory]
    [InlineData("寿命到期，正常更换", WearPartReplacementReason.Normal)]
    [InlineData("过程损坏", WearPartReplacementReason.ProcessDamage)]
    [InlineData("切拉换型", WearPartReplacementReason.Cutover)]
    [InlineData("寿命到期，更换位置", WearPartReplacementReason.ChangePosition)]
    [InlineData("寿命到期维保", WearPartReplacementReason.Maintenance)]
    public void NormalizeReplacementReason_ShouldMapLegacyMessages(string legacyValue, string expectedValue)
    {
        var normalized = LegacyImportValueConverter.NormalizeReplacementReason(legacyValue);

        Assert.Equal(expectedValue, normalized);
    }

    [Theory]
    [InlineData("LReal", PartDataType.Double)]
    [InlineData("Real", PartDataType.Float)]
    [InlineData("Int", PartDataType.Int16)]
    [InlineData("DInt", PartDataType.Int)]
    [InlineData("UDInt", PartDataType.UInt32)]
    [InlineData("Bool", PartDataType.Bool)]
    public void ResolvePartDataType_ShouldAcceptLegacyAliases(string dataType, PartDataType expectedType)
    {
        var resolved = WearPartPlcAccessor.ResolvePartDataType(dataType);

        Assert.Equal(expectedType, resolved);
    }
}
