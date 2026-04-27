using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WearPartsControl.Views;

internal static class NotificationMarkdownDocumentBuilder
{
    public static FlowDocument Build(string markdown)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 14,
            Foreground = CreateBrush("#1F3348"),
            LineHeight = 22
        };

        List? currentList = null;

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                currentList = null;
                continue;
            }

            if (trimmed == "---")
            {
                currentList = null;
                document.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 10, 0, 10),
                    Background = CreateBrush("#D5E0EC")
                }));
                continue;
            }

            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                currentList = null;
                document.Blocks.Add(new Paragraph(new Run(trimmed[2..]))
                {
                    FontSize = 28,
                    FontWeight = FontWeights.Bold,
                    Foreground = CreateBrush("#1E3550"),
                    Margin = new Thickness(0, 0, 0, 14)
                });
                continue;
            }

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                currentList = null;
                document.Blocks.Add(new Paragraph(new Run(trimmed[3..]))
                {
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = CreateBrush("#214769"),
                    Margin = new Thickness(0, 12, 0, 8)
                });
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                currentList ??= new List
                {
                    MarkerStyle = TextMarkerStyle.Disc,
                    Margin = new Thickness(18, 0, 0, 0)
                };

                currentList.ListItems.Add(new ListItem(CreateParagraph(trimmed[2..], new Thickness(0, 0, 0, 2))));

                if (!document.Blocks.Contains(currentList))
                {
                    document.Blocks.Add(currentList);
                }

                continue;
            }

            currentList = null;
            document.Blocks.Add(CreateParagraph(trimmed, new Thickness(0, 0, 0, 2)));
        }

        return document;
    }

    private static Paragraph CreateParagraph(string content, Thickness margin)
    {
        var paragraph = new Paragraph
        {
            Margin = margin,
            LineHeight = 22
        };

        var labelStart = content.IndexOf("**", StringComparison.Ordinal);
        var labelEnd = labelStart >= 0
            ? content.IndexOf("**", labelStart + 2, StringComparison.Ordinal)
            : -1;

        if (labelStart == 0 && labelEnd > labelStart)
        {
            var label = content.Substring(labelStart + 2, labelEnd - labelStart - 2);
            var remainder = content[(labelEnd + 2)..];
            paragraph.Inlines.Add(new Bold(new Run(label)));
            if (!string.IsNullOrEmpty(remainder))
            {
                paragraph.Inlines.Add(new Run(remainder));
            }

            return paragraph;
        }

        paragraph.Inlines.Add(new Run(content));
        return paragraph;
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }
}