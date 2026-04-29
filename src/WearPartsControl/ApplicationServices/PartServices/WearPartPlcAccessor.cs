using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

internal static class WearPartPlcAccessor
{
    private const string SkipAddress = "######";

    public static PlcConnectionOptions BuildConnectionOptions(ClientAppConfigurationEntity configuration)
    {
        return PlcConnectionOptionsFactory.Create(configuration);
    }

    public static async Task<string> ReadAsStringAsync(IPlcOperationPipeline pipeline, string operationName, string address, string dataType, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return string.Empty;
        }

        return NormalizeDataType(dataType) switch
        {
            WearPartPlcDataTypes.Int16 => (await pipeline.ReadAsync<short>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            WearPartPlcDataTypes.Int32 => (await pipeline.ReadAsync<int>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            WearPartPlcDataTypes.UInt32 => (await pipeline.ReadAsync<uint>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            WearPartPlcDataTypes.Float => (await pipeline.ReadAsync<float>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            WearPartPlcDataTypes.Double => (await pipeline.ReadAsync<double>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            WearPartPlcDataTypes.Bool => await pipeline.ReadAsync<bool>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false) ? "1" : "0",
            _ => await pipeline.ReadAsync<string>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false)
        };
    }

    public static async Task<double> ReadAsDoubleAsync(IPlcOperationPipeline pipeline, string operationName, string address, string dataType, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return 0d;
        }

        return NormalizeDataType(dataType) switch
        {
            WearPartPlcDataTypes.Int16 => await pipeline.ReadAsync<short>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false),
            WearPartPlcDataTypes.Int32 => await pipeline.ReadAsync<int>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false),
            WearPartPlcDataTypes.UInt32 => await pipeline.ReadAsync<uint>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false),
            WearPartPlcDataTypes.Float => await pipeline.ReadAsync<float>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false),
            WearPartPlcDataTypes.Double => await pipeline.ReadAsync<double>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false),
            WearPartPlcDataTypes.Bool => await pipeline.ReadAsync<bool>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false) ? 1d : 0d,
            _ => ParseDouble(await pipeline.ReadAsync<string>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false), address)
        };
    }

    public static Task ClearCounterAsync(IPlcOperationPipeline pipeline, string operationName, string address, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return Task.CompletedTask;
        }

        return pipeline.WriteAsync(operationName, address, 0, cancellationToken: cancellationToken);
    }

    public static async Task PulseZeroClearSignalAsync(IPlcOperationPipeline pipeline, string operationName, string address, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return;
        }

        await pipeline.WriteAsync(operationName, address, true, cancellationToken: cancellationToken).ConfigureAwait(false);
        await pipeline.WriteAsync(operationName, address, false, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public static Task WriteCurrentValueAsync(IPlcOperationPipeline pipeline, string operationName, string address, string dataType, double value, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return Task.CompletedTask;
        }

        switch (NormalizeDataType(dataType))
        {
            case WearPartPlcDataTypes.Int16:
                return pipeline.WriteAsync(address: address, operationName: operationName, value: Convert.ToInt16(Math.Round(value, MidpointRounding.AwayFromZero), System.Globalization.CultureInfo.InvariantCulture), cancellationToken: cancellationToken);
            case WearPartPlcDataTypes.Int32:
                return pipeline.WriteAsync(address: address, operationName: operationName, value: Convert.ToInt32(Math.Round(value, MidpointRounding.AwayFromZero), System.Globalization.CultureInfo.InvariantCulture), cancellationToken: cancellationToken);
            case WearPartPlcDataTypes.UInt32:
                return pipeline.WriteAsync(address: address, operationName: operationName, value: Convert.ToUInt32(Math.Round(value, MidpointRounding.AwayFromZero), System.Globalization.CultureInfo.InvariantCulture), cancellationToken: cancellationToken);
            case WearPartPlcDataTypes.Float:
                return pipeline.WriteAsync(address: address, operationName: operationName, value: Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture), cancellationToken: cancellationToken);
            case WearPartPlcDataTypes.Double:
                return pipeline.WriteAsync(address: address, operationName: operationName, value: value, cancellationToken: cancellationToken);
            case WearPartPlcDataTypes.Bool:
                return pipeline.WriteAsync(address: address, operationName: operationName, value: value > 0d, cancellationToken: cancellationToken);
            default:
                return pipeline.WriteAsync(address: address, operationName: operationName, value: value.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken: cancellationToken);
        }
    }

    public static Task WriteBarcodeAsync(IPlcOperationPipeline pipeline, string operationName, string address, string barcode, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return Task.CompletedTask;
        }

        return pipeline.WriteAsync(operationName, address, barcode, cancellationToken: cancellationToken);
    }

    public static Task WriteShutdownSignalAsync(IPlcOperationPipeline pipeline, string operationName, string address, bool shutdown, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return Task.CompletedTask;
        }

        var trimmed = address.Trim();
        var invert = trimmed.StartsWith('!');
        var actualAddress = invert ? trimmed[1..] : trimmed;
        return pipeline.WriteAsync(operationName, actualAddress, invert ? !shutdown : shutdown, cancellationToken: cancellationToken);
    }

    public static PartDataType? ResolvePartDataType(string? dataType)
    {
        return NormalizeDataType(dataType) switch
        {
            WearPartPlcDataTypes.Json => PartDataType.Json,
            WearPartPlcDataTypes.String => PartDataType.String,
            WearPartPlcDataTypes.Int16 => PartDataType.Int16,
            WearPartPlcDataTypes.Int32 => PartDataType.Int,
            WearPartPlcDataTypes.UInt32 => PartDataType.UInt32,
            WearPartPlcDataTypes.Float => PartDataType.Float,
            WearPartPlcDataTypes.Double => PartDataType.Double,
            WearPartPlcDataTypes.Bool => PartDataType.Bool,
            _ => null
        };
    }

    private static bool ShouldSkip(string address)
    {
        return string.IsNullOrWhiteSpace(address) || string.Equals(address.Trim(), SkipAddress, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDataType(string? dataType)
    {
        return WearPartPlcDataTypes.Normalize(dataType, string.Empty);
    }

    private static double ParseDouble(string rawValue, string address)
    {
        if (double.TryParse(rawValue, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (double.TryParse(rawValue, out value))
        {
            return value;
        }

        throw new UserFriendlyException(LocalizedText.Format("Services.WearPartReplacement.ValueCannotConvert", address, rawValue));
    }
}