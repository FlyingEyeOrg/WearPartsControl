using System.Globalization;
using System.Text;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Domain.Entities;

namespace WearPartsControl.ApplicationServices.ComNotification;

public sealed record ComNotificationMessage(string Title, string Markdown);

public static class ComNotificationMessageFactory
{
    private const string TemplatePrefix = "ViewModels.ComNotificationTemplate.";

    public static ComNotificationMessage CreateTestMessage(
        ClientAppInfoModel? clientAppInfo,
        string? meResponsibleWorkId,
        string? prdResponsibleWorkId)
    {
        var title = LocalizedText.Get("ViewModels.UserConfigVm.TestNotificationTitle");
        var markdown = BuildMarkdown(
            Template("TestHeading"),
            CreateEnvironment(clientAppInfo),
            Template("NotificationInfoHeading"),
            [
                new NotificationItem(
                    Template("DescriptionLabel"),
                    LocalizedText.Get("ViewModels.UserConfigVm.TestNotificationBody"))
            ],
            meResponsibleWorkId,
            prdResponsibleWorkId,
            Template("TestActionMessage"),
            DateTime.Now);

        return new ComNotificationMessage(title, markdown);
    }

    public static ComNotificationMessage CreateWearPartAlertMessage(
        ClientAppConfigurationEntity clientAppConfiguration,
        string severity,
        string partName,
        double currentValue,
        double warningValue,
        double shutdownValue,
        string? meResponsibleWorkId,
        string? prdResponsibleWorkId,
        DateTime occurredAt)
    {
        var isShutdown = string.Equals(severity, "Shutdown", StringComparison.OrdinalIgnoreCase);
        var title = LocalizedText.Get(isShutdown
            ? "Services.WearPartMonitor.ShutdownNotificationTitle"
            : "Services.WearPartMonitor.WarningNotificationTitle");
        var markdown = BuildMarkdown(
            Template(isShutdown
                ? "ShutdownHeading"
                : "WarningHeading"),
            CreateEnvironment(clientAppConfiguration),
            Template("WearPartInfoHeading"),
            [
                new NotificationItem(Template("PartNameLabel"), ResolveDisplayValue(partName)),
                new NotificationItem(Template("CurrentValueLabel"), FormatNumber(currentValue)),
                new NotificationItem(Template("WarningValueLabel"), FormatNumber(warningValue)),
                new NotificationItem(Template("ShutdownValueLabel"), FormatNumber(shutdownValue))
            ],
            meResponsibleWorkId,
            prdResponsibleWorkId,
            Template(isShutdown
                ? "ShutdownActionMessage"
                : "WarningActionMessage"),
            occurredAt.ToLocalTime());

        return new ComNotificationMessage(title, markdown);
    }

    private static string BuildMarkdown(
        string heading,
        NotificationEnvironment environment,
        string detailHeading,
        IReadOnlyList<NotificationItem> detailItems,
        string? meResponsibleWorkId,
        string? prdResponsibleWorkId,
        string actionMessage,
        DateTime occurredAt)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {heading}");
        builder.AppendLine();
        AppendSummaryLine(builder, Template("TimeLabel"), occurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        AppendSummaryLine(builder, Template("SiteLabel"), environment.SiteCode);
        AppendSummaryLine(builder, Template("FactoryLabel"), environment.FactoryCode);
        AppendSummaryLine(builder, Template("AreaLabel"), environment.AreaCode);
        AppendSummaryLine(builder, Template("ProcedureLabel"), environment.ProcedureCode);
        AppendSummaryLine(builder, Template("EquipmentCodeLabel"), environment.EquipmentCode);
        AppendSummaryLine(builder, Template("ResourceNumberLabel"), environment.ResourceNumber);
        builder.AppendLine();
        AppendSection(builder, detailHeading, detailItems);
        builder.AppendLine();
        AppendSection(
            builder,
            Template("OwnerInfoHeading"),
            [
                new NotificationItem(Template("MeResponsibleLabel"), ResolveDisplayValue(meResponsibleWorkId)),
                new NotificationItem(Template("PrdResponsibleLabel"), ResolveDisplayValue(prdResponsibleWorkId))
            ]);
        builder.AppendLine();
        builder.AppendLine(actionMessage);
        builder.AppendLine();
        builder.AppendLine("---");
        builder.Append(Template("Footer"));
        return builder.ToString();
    }

    private static void AppendSummaryLine(StringBuilder builder, string label, string? value)
    {
        builder.AppendLine($"**{label}**: {ResolveDisplayValue(value)}  ");
    }

    private static void AppendSection(StringBuilder builder, string heading, IReadOnlyList<NotificationItem> items)
    {
        builder.AppendLine($"## {heading}");
        builder.AppendLine();
        foreach (var item in items)
        {
            builder.AppendLine($"- **{item.Label}**: {item.Value}");
        }
    }

    private static NotificationEnvironment CreateEnvironment(ClientAppInfoModel? clientAppInfo)
    {
        return new NotificationEnvironment(
            clientAppInfo?.SiteCode,
            clientAppInfo?.FactoryCode,
            clientAppInfo?.AreaCode,
            clientAppInfo?.ProcedureCode,
            clientAppInfo?.EquipmentCode,
            clientAppInfo?.ResourceNumber);
    }

    private static NotificationEnvironment CreateEnvironment(ClientAppConfigurationEntity clientAppConfiguration)
    {
        return new NotificationEnvironment(
            clientAppConfiguration.SiteCode,
            clientAppConfiguration.FactoryCode,
            clientAppConfiguration.AreaCode,
            clientAppConfiguration.ProcedureCode,
            clientAppConfiguration.EquipmentCode,
            clientAppConfiguration.ResourceNumber);
    }

    private static string ResolveDisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Template("NotConfigured")
            : value.Trim();
    }

    private static string Template(string name)
    {
        return LocalizedText.Get($"{TemplatePrefix}{name}");
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private sealed record NotificationEnvironment(
        string? SiteCode,
        string? FactoryCode,
        string? AreaCode,
        string? ProcedureCode,
        string? EquipmentCode,
        string? ResourceNumber);

    private sealed record NotificationItem(string Label, string Value);
}