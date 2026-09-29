using System.Net.Http;
using System.IO;

namespace UsbLlm.Services;

public sealed record ModelDownloadProgress(string FileName, long DownloadedBytes, long? TotalBytes);

public static class ModelStorage
{
    public static string Root
    {
        get
        {
            var executableDirectory = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? AppContext.BaseDirectory;
            var portableRoot = Path.Combine(executableDirectory, "Models");
            return Directory.Exists(portableRoot)
                ? portableRoot
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UsbLLM", "Models");
        }
    }

    public static string GetModelPath(ModelDefinition model) => Path.Combine(Root, model.ModelFile);
    public static string? GetProjectorPath(ModelDefinition model) => model.VisionProjectorFile is null ? null : Path.Combine(Root, model.VisionProjectorFile);
}

public sealed class ModelDownloadService
{
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };

    public async Task DownloadAsync(ModelDefinition model, IProgress<ModelDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        await DownloadFileAsync(model.QuantizedSource, model.ModelFile, progress, cancellationToken);
        if (model.VisionProjectorFile is not null)
            await DownloadFileAsync(model.QuantizedSource, model.VisionProjectorFile, progress, cancellationToken);
    }

    private static async Task DownloadFileAsync(string repository, string relativePath, IProgress<ModelDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var destination = Path.Combine(ModelStorage.Root, relativePath);
        if (File.Exists(destination) && new FileInfo(destination).Length > 0) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".part";
        var fileName = Path.GetFileName(relativePath);
        var uri = new Uri($"https://huggingface.co/{repository}/resolve/main/{Uri.EscapeDataString(fileName)}?download=true");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("UsbLLM/1.0");
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using (var target = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true))
        {
            var buffer = new byte[1024 * 1024];
            long downloaded = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                downloaded += read;
                progress?.Report(new ModelDownloadProgress(fileName, downloaded, response.Content.Headers.ContentLength));
            }
            await target.FlushAsync(cancellationToken);
        }
        File.Move(temporary, destination, overwrite: true);
    }
}
