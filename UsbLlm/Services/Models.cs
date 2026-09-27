using System.Runtime.InteropServices;
using System.Text.Json;
using System.IO;
using System.Diagnostics;

namespace UsbLlm.Services;

public sealed record ModelDefinition(
    string Id, string DisplayName, string Label, string Source, string QuantizedSource,
    string ModelFile, string? VisionProjectorFile, double SizeGb, int MinimumRamGb,
    int MinimumVramGb, bool SupportsVision, bool SupportsImageGeneration)
{
    public string DisplayLabel => $"{DisplayName} — {Label}";
}

public sealed class ModelCatalog
{
    public IReadOnlyList<ModelDefinition> Models { get; }

    private ModelCatalog(IReadOnlyList<ModelDefinition> models) => Models = models;

    public static ModelCatalog Load(string applicationDirectory)
    {
        var path = Path.Combine(applicationDirectory, "Models", "models.manifest.json");
        if (!File.Exists(path)) return new ModelCatalog([]);

        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<ModelManifest>(json, JsonOptions)
            ?? throw new InvalidDataException("Model catalog could not be read.");
        return new ModelCatalog(document.Models ?? []);
    }

    public ModelDefinition? Recommend(HardwareProfile hardware) =>
        Models.Where(model => model.MinimumRamGb <= hardware.SystemRamGb)
              .Where(model => hardware.GpuVramGb is null || model.MinimumVramGb <= hardware.GpuVramGb || hardware.GpuVramGb == 0)
              .OrderByDescending(model => model.MinimumRamGb)
              .FirstOrDefault()
        ?? Models.FirstOrDefault();

    private sealed class ModelManifest { public List<ModelDefinition>? Models { get; init; } }
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
}

public sealed record HardwareProfile(int SystemRamGb, int? GpuVramGb, string Accelerator);

public static class HardwareProbe
{
    public static HardwareProfile Detect()
    {
        var state = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        GlobalMemoryStatusEx(ref state);
        var ram = Math.Max(1, (int)Math.Floor(state.TotalPhysical / 1024d / 1024 / 1024));
        var gpuVram = TryReadNvidiaVramGb();
        var accelerator = gpuVram is { } vram
            ? $"NVIDIA GPU detected ({vram} GB VRAM); CUDA preferred"
            : "CUDA/Vulkan are selected by the bundled runtime when present";
        return new HardwareProfile(ram, gpuVram, accelerator);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    private static int? TryReadNvidiaVramGb()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=memory.total --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null || !process.WaitForExit(1500) || process.ExitCode != 0) return null;
            var firstValue = process.StandardOutput.ReadLine();
            return int.TryParse(firstValue, out var megabytes) ? (int)Math.Floor(megabytes / 1024d) : null;
        }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
