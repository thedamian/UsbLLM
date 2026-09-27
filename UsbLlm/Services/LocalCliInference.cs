using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace UsbLlm.Services;

public sealed class LocalCliInference
{
    public async Task<string> GenerateAsync(ModelDefinition model, IEnumerable<ChatMessage> messages,
        IEnumerable<LocalAttachment> attachments, CancellationToken cancellationToken)
    {
        var modelPath = ModelStorage.GetModelPath(model);
        var projectorPath = ModelStorage.GetProjectorPath(model);
        var runtime = RuntimeLocator.Locate();
        if (runtime is null) return "The local runtime is not available in this build. No prompt was sent anywhere.";
        if (!File.Exists(modelPath)) return $"{model.DisplayName} is not on this USB drive yet. Choose a downloaded model instead.";
        if (model.SupportsVision && projectorPath is not null && !File.Exists(projectorPath)) return $"The vision component for {model.DisplayName} is missing.";

        var info = new ProcessStartInfo
        {
            FileName = runtime.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add("--model");
        info.ArgumentList.Add(modelPath);
        if (projectorPath is not null)
        {
            info.ArgumentList.Add("--mmproj");
            info.ArgumentList.Add(projectorPath);
        }
        foreach (var image in attachments.Where(item => item.IsImage).Take(4))
        {
            if (!model.SupportsVision) continue;
            info.ArgumentList.Add("--image");
            info.ArgumentList.Add(image.Path);
        }
        info.ArgumentList.Add("--prompt");
        info.ArgumentList.Add(PromptComposer.Compose(messages, attachments));
        info.ArgumentList.Add("--single-turn");
        info.ArgumentList.Add("--simple-io");
        info.ArgumentList.Add("--no-display-prompt");
        info.ArgumentList.Add("--n-predict");
        info.ArgumentList.Add("768");

        using var process = Process.Start(info) ?? throw new InvalidOperationException("The private inference process could not start.");
        using var registration = cancellationToken.Register(() =>
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        });
        var answerTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var answer = (await answerTask).Trim();
        var error = await errorTask;
        if (process.ExitCode != 0)
            return $"The local {runtime.Backend} runtime could not complete this request (exit {process.ExitCode}). {Shorten(error)}";
        return string.IsNullOrWhiteSpace(answer) ? "The local model returned no text." : answer;
    }

    private static string Shorten(string text) => Regex.Replace(text, "\\s+", " ").Trim() is { Length: > 300 } value ? value[..300] : Regex.Replace(text, "\\s+", " ").Trim();
}

internal static class PromptComposer
{
    public static string Compose(IEnumerable<ChatMessage> messages, IEnumerable<LocalAttachment> attachments)
    {
        var conversation = new StringBuilder("You are Usb LLM, a private offline assistant. Answer helpfully and clearly.\n\n");
        foreach (var message in messages.TakeLast(18))
            conversation.Append(message.Speaker).Append(": ").Append(message.RawText).Append("\n\n");
        foreach (var text in attachments.Where(item => !item.IsImage).Select(AttachmentTextExtractor.Extract).Where(text => !string.IsNullOrWhiteSpace(text)))
            conversation.Append("Local attachment:\n").Append(text).Append("\n\n");
        conversation.Append("Assistant:");
        return conversation.ToString();
    }
}

internal static class AttachmentTextExtractor
{
    private const int Limit = 80_000;

    public static string Extract(LocalAttachment attachment)
    {
        try
        {
            var extension = Path.GetExtension(attachment.Path).ToLowerInvariant();
            return extension switch
            {
                ".txt" or ".md" or ".csv" or ".json" => Trim(File.ReadAllText(attachment.Path)),
                ".docx" => ExtractDocx(attachment.Path),
                ".pdf" => "[PDF attached. Native PDF extraction is enabled by the complete document adapter.]",
                _ => $"[{attachment.DisplayName} is attached locally.]"
            };
        }
        catch { return $"[{attachment.DisplayName} could not be read locally.]"; }
    }

    private static string ExtractDocx(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var document = archive.GetEntry("word/document.xml");
        if (document is null) return "[The DOCX contains no readable document XML.]";
        using var reader = new StreamReader(document.Open());
        var xml = reader.ReadToEnd();
        return Trim(System.Net.WebUtility.HtmlDecode(Regex.Replace(xml, "<[^>]+>", " ")));
    }

    private static string Trim(string text) => Regex.Replace(text, "\\s+", " ").Trim() is { Length: > Limit } value ? value[..Limit] : Regex.Replace(text, "\\s+", " ").Trim();
}
