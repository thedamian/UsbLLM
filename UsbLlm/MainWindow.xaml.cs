using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.IO;
using UsbLlm.Services;

namespace UsbLlm;

public partial class MainWindow : Window
{
    private readonly ModelCatalog _catalog;
    private readonly HardwareProfile _hardware;
    public ObservableCollection<ChatSession> Sessions { get; } = [];
    public ObservableCollection<ChatMessage> ActiveMessages { get; } = [];
    public ObservableCollection<LocalAttachment> ActiveAttachments { get; } = [];
    private ChatSession? ActiveSession => SessionsList.SelectedItem as ChatSession;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _catalog = ModelCatalog.Load(AppContext.BaseDirectory);
        _hardware = HardwareProbe.Detect();
        HardwareSummary.Text = $"{_hardware.SystemRamGb} GB RAM · {_hardware.Accelerator} · {RuntimeLocator.Describe()}";
        ModelSelector.ItemsSource = _catalog.Models;
        ModelSelector.SelectedItem = _catalog.Recommend(_hardware);
        NewSession();
    }

    private void NewChat_Click(object sender, RoutedEventArgs e) => NewSession();

    private void NewSession()
    {
        var session = new ChatSession($"Chat {Sessions.Count + 1}");
        session.Messages.Add(new ChatMessage("Usb LLM", "Private session ready. Nothing in this conversation is saved automatically."));
        Sessions.Add(session);
        SessionsList.SelectedItem = session;
    }

    private void SessionsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ActiveMessages.Clear();
        ActiveAttachments.Clear();
        if (ActiveSession is null) return;
        ChatTitle.Text = ActiveSession.Title;
        foreach (var message in ActiveSession.Messages) ActiveMessages.Add(message);
        foreach (var attachment in ActiveSession.Attachments) ActiveAttachments.Add(attachment);
    }

    private void ModelSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ModelSelector.SelectedItem is ModelDefinition model)
            Title = $"Usb LLM — {model.DisplayName}";
    }

    private void Attach_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is null) return;
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "Supported files|*.txt;*.md;*.csv;*.json;*.docx;*.pdf;*.png;*.jpg;*.jpeg;*.webp|All files|*.*" };
        if (dialog.ShowDialog(this) != true) return;

        foreach (var path in dialog.FileNames)
        {
            var attachment = LocalAttachment.FromPath(path);
            ActiveSession.Attachments.Add(attachment);
            ActiveAttachments.Add(attachment);
        }
    }

    private void GenerateImage_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is null) return;
        if (ModelSelector.SelectedItem is not ModelDefinition { SupportsImageGeneration: true } model)
        {
            AddMessage(new ChatMessage("Usb LLM", "The selected local model can understand images but does not advertise local image generation."));
            return;
        }
        AddMessage(new ChatMessage("Usb LLM", $"{model.DisplayName} supports image generation. Its local image generation adapter will create the result without an Internet request."));
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is null || string.IsNullOrWhiteSpace(PromptBox.Text)) return;
        var prompt = PromptBox.Text.Trim();
        AddMessage(new ChatMessage("You", prompt));
        PromptBox.Clear();
        if (ModelSelector.SelectedItem is not ModelDefinition model)
        {
            AddMessage(new ChatMessage("Usb LLM", "No local model is selected."));
            return;
        }
        SendButton.IsEnabled = false;
        try
        {
            var answer = await new LocalCliInference().GenerateAsync(model, ActiveSession.Messages, ActiveSession.Attachments, CancellationToken.None);
            AddMessage(new ChatMessage(model.DisplayName, answer));
        }
        catch (Exception exception)
        {
            AddMessage(new ChatMessage("Usb LLM", $"The private runtime stopped safely: {exception.Message}"));
        }
        finally { SendButton.IsEnabled = true; }
    }

    private void AddMessage(ChatMessage message)
    {
        if (ActiveSession is null) return;
        ActiveSession.Messages.Add(message);
        ActiveMessages.Add(message);
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string code && code.Length > 0) Clipboard.SetText(code);
    }

    private void ExportCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is not null) Export("UsbLLM-chat.md", ActiveSession.ToMarkdown());
    }

    private void ExportAll_Click(object sender, RoutedEventArgs e)
    {
        Export("UsbLLM-session-history.md", string.Join("\n\n---\n\n", Sessions.Select(session => session.ToMarkdown())));
    }

    private void Export(string suggestedName, string contents)
    {
        var dialog = new SaveFileDialog { FileName = suggestedName, Filter = "Markdown|*.md|Text|*.txt" };
        if (dialog.ShowDialog(this) == true) File.WriteAllText(dialog.FileName, contents, new UTF8Encoding(false));
    }
}

public sealed class ChatSession(string title)
{
    public string Title { get; } = title;
    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<LocalAttachment> Attachments { get; } = [];
    public string ToMarkdown() => $"# {Title}\n\n" + string.Join("\n\n", Messages.Select(message => $"## {message.Speaker}\n\n{message.Body}"));
}

public sealed class ChatMessage
{
    public string Speaker { get; }
    public string RawText { get; }
    public string Body { get; }
    public string Code { get; }
    public Visibility CodeVisibility => string.IsNullOrWhiteSpace(Code) ? Visibility.Collapsed : Visibility.Visible;

    public ChatMessage(string speaker, string text)
    {
        Speaker = speaker;
        RawText = text;
        var blocks = text.Split("```");
        Body = blocks[0].Trim();
        Code = blocks.Length >= 3 ? blocks[1].Trim().TrimStart('\r', '\n') : string.Empty;
    }
}

public sealed record LocalAttachment(string Path, string DisplayName, bool IsImage)
{
    public static LocalAttachment FromPath(string path) => new(path, System.IO.Path.GetFileName(path),
        new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));
}
