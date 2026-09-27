using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace UsbLlm;

/// <summary>Small, dependency-free presentation layer for common chat Markdown.</summary>
public sealed class MarkdownText : TextBlock
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown), typeof(string), typeof(MarkdownText),
        new PropertyMetadata(string.Empty, static (control, _) => ((MarkdownText)control).Render()));

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private void Render()
    {
        Inlines.Clear();
        var lines = Markdown.Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.StartsWith("# "))
            {
                AddInlineLine(line[2..], FontWeights.SemiBold, 20, Brushes.White);
            }
            else if (line.StartsWith("## "))
            {
                AddInlineLine(line[3..], FontWeights.SemiBold, 17, Brushes.White);
            }
            else if (line.StartsWith("> "))
            {
                AddInlineLine("│ " + line[2..], FontWeights.Normal, FontSize, new SolidColorBrush(Color.FromRgb(148, 163, 184)));
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                AddInlineLine("• " + line[2..], FontWeights.Normal, FontSize, Foreground);
            }
            else
            {
                AddInlineLine(line, FontWeights.Normal, FontSize, Foreground);
            }
            if (index < lines.Length - 1) Inlines.Add(new LineBreak());
        }
    }

    private void AddInlineLine(string line, FontWeight weight, double size, Brush? brush)
    {
        var position = 0;
        while (position < line.Length)
        {
            var codeStart = line.IndexOf('`', position);
            if (codeStart < 0)
            {
                Inlines.Add(new Run(line[position..]) { FontWeight = weight, FontSize = size, Foreground = brush });
                break;
            }
            if (codeStart > position) Inlines.Add(new Run(line[position..codeStart]) { FontWeight = weight, FontSize = size, Foreground = brush });
            var codeEnd = line.IndexOf('`', codeStart + 1);
            if (codeEnd < 0)
            {
                Inlines.Add(new Run(line[codeStart..]) { FontWeight = weight, FontSize = size, Foreground = brush });
                break;
            }
            Inlines.Add(new Run(line[(codeStart + 1)..codeEnd])
            {
                FontFamily = new FontFamily("Cascadia Code"),
                Background = new SolidColorBrush(Color.FromRgb(2, 6, 23)),
                Foreground = new SolidColorBrush(Color.FromRgb(153, 246, 228))
            });
            position = codeEnd + 1;
        }
    }
}
