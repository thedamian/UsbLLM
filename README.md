# Usb LLM

**Real Privacy with no Internet and no logging but full power**

Usb LLM is a portable, Windows-first local chat application.  It starts from a
USB drive, loads models that live beside the executable, and keeps chat state
only in memory unless the person using it deliberately exports a conversation.

## Portable layout

The release drive contains one application file and a model folder:

```text
UsbLLM.exe
Models/
  models.manifest.json
  Qwen3.5-0.8B/
  ...
```

The executable remains one self-contained file. Model weights cannot reasonably
be embedded in that file: the six supplied models total about 53.5 GB even in
their USB-friendly quantized form. The application never copies models into a
user profile. A bundled inference native library may extract into a hidden,
temporary runtime directory for Windows loader compatibility; its contents are
removed during normal shutdown.

## Architecture

1. **WPF shell**: fullscreen chat UI, an in-memory session list, attachments,
   model selector, Markdown/code presentation, and explicit export commands.
2. **Session-only domain**: chat messages, attachment text, and system context
   are never written automatically. Closing the process disposes every session.
3. **Portable model catalog**: `Models/models.manifest.json` describes the
   model file, vision projector, supported features, and safe RAM/VRAM guidance.
4. **Local runtime bridge**: the production build launches a bundled
   `llama.cpp` runtime with CUDA, Vulkan, then CPU fallback. It accepts only
   loopback pipes/stdio and has no HTTP client or telemetry path.
5. **Content adapters**: document text is extracted locally and offered to the
   model as context; images use the selected model's local vision projector.
   Image generation is enabled only when a selected local model/runtime advertises
   that capability.

## Model artifacts

The original supplied Hugging Face repositories contain BF16 Transformers
weights, which would exceed the USB target and cannot be loaded directly by the
portable native runtime. Usb LLM therefore uses the corresponding `unsloth`
Q4_K_M GGUF releases plus one BF16 vision projector each. These preserve the
specified source model lineage while making the whole catalog practical to carry.

Run `dotnet publish UsbLlm/UsbLlm.csproj -c Release -r win-x64` to create the
single-file Windows x64 app once the inference runtime is bundled.

To stage a prepared USB drive, use `tools/Stage-PortableUsb.ps1 -Destination E:\`.

