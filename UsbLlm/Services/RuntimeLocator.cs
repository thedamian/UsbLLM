using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace UsbLlm.Services;

public sealed record RuntimeCandidate(string Backend, string ExecutablePath);

public static class RuntimeLocator
{
    public static RuntimeCandidate? Locate()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Runtime");
        var candidates = new List<(string Backend, string Folder, bool Enabled)>
        {
            ("CUDA", "CUDA", HasNvidiaGpu()),
            ("Vulkan", "Vulkan", HasVulkan()),
            ("CPU", "CPU", true)
        };

        foreach (var candidate in candidates.Where(candidate => candidate.Enabled))
        {
            var executable = Path.Combine(root, candidate.Folder, "llama-cli.exe");
            if (File.Exists(executable)) return new RuntimeCandidate(candidate.Backend, executable);
        }
        return null;
    }

    public static string Describe() => Locate() is { } runtime
        ? $"{runtime.Backend} local runtime ready"
        : "Local runtime will use CUDA, Vulkan, then CPU";

    private static bool HasVulkan()
    {
        if (!NativeLibrary.TryLoad("vulkan-1.dll", out var handle)) return false;
        NativeLibrary.Free(handle);
        return true;
    }

    private static bool HasNvidiaGpu()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name --format=csv,noheader",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return process is not null && process.WaitForExit(1500) && process.ExitCode == 0 && !string.IsNullOrWhiteSpace(process.StandardOutput.ReadToEnd());
        }
        catch { return false; }
    }
}
