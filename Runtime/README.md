# Bundled local runtime

Release packaging places the official `llama.cpp` Windows x64 CPU, CUDA, and
Vulkan builds here before publishing. `UsbLlm.exe` embeds these files and
extracts them only to the .NET single-file runtime location when it starts.

Run `tools/Download-Runtimes.ps1` to fetch current artifacts for a release.
