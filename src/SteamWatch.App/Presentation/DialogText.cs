using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SteamWatch.App.Presentation;

internal static class DialogText
{
    public static TextBlock Body(string text) => new()
    {
        Text = text, LineHeight = 24, TextWrapping = TextWrapping.Wrap
    };

    public static StackPanel Paragraphs(params string[] paragraphs)
    {
        var panel = new StackPanel { Spacing = 12 };
        foreach (var paragraph in paragraphs) panel.Children.Add(Body(paragraph));
        return panel;
    }

    // Format only caller-owned content; leave dialog titles and control templates alone.
    public static object? Format(object? content)
    {
        switch (content)
        {
            case string text:
                return Paragraphs(text.Replace("\r\n", "\n").Split('\n'));
            case TextBlock text:
                text.LineHeight = 24;
                text.TextWrapping = TextWrapping.Wrap;
                break;
            case Panel panel:
                foreach (var child in panel.Children) Format(child);
                break;
            case ScrollViewer scroll:
                scroll.Content = Format(scroll.Content);
                break;
            case ItemsControl items when items.ItemsSource is System.Collections.IEnumerable source:
                foreach (var item in source) Format(item);
                break;
        }
        return content;
    }
}
