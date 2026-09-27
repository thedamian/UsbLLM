[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$bootstrapBuild = Join-Path $projectRoot 'bootstrapper\build'
cmake -S (Join-Path $projectRoot 'bootstrapper') -B $bootstrapBuild -A x64
cmake --build $bootstrapBuild --config Release

$inner = Join-Path $projectRoot 'artifacts\UsbLLM-Download\UsbLlm.exe'
$bootstrap = Join-Path $bootstrapBuild 'Release\UsbLlmBootstrap.exe'
$models = Join-Path $projectRoot 'Models'
$output = Join-Path $projectRoot 'artifacts\UsbLLM-Offline.exe'
if (-not (Test-Path $inner) -or -not (Test-Path $bootstrap)) { throw 'Build the Download edition before packaging the Offline edition.' }
$files = @($inner, (Join-Path $models 'models.manifest.json')) + (Get-ChildItem $models -Recurse -File -Filter '*.gguf' | Select-Object -ExpandProperty FullName)
if ($files.Count -ne 14) { throw "Expected 14 GGUF files, found $($files.Count - 2). Complete every model download before packaging." }

$rootUri = [Uri]($projectRoot + [IO.Path]::DirectorySeparatorChar)
$entries = [Collections.Generic.List[string]]::new()
$stream = [IO.File]::Open($output, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
  $bootstrapStream = [IO.File]::OpenRead($bootstrap); $bootstrapStream.CopyTo($stream); $bootstrapStream.Dispose()
  foreach ($file in $files) {
    $path = if ($file -eq $inner) { 'UsbLlm.exe' } else { ([Uri]$file).MakeRelativeUri($rootUri).ToString().Replace('/', '\') }
    $offset = $stream.Position; $source = [IO.File]::OpenRead($file); $source.CopyTo($stream); $length = $source.Length; $source.Dispose()
    $entries.Add("$path|$offset|$length")
  }
  $index = "build=offline-1`n" + ($entries -join "`n") + "`n"
  $indexBytes = [Text.Encoding]::UTF8.GetBytes($index); $stream.Write($indexBytes, 0, $indexBytes.Length)
  $magic = [Text.Encoding]::ASCII.GetBytes("USBLLM1`0"); $stream.Write($magic, 0, 8)
  $lengthBytes = [BitConverter]::GetBytes([Int64]$indexBytes.Length); $stream.Write($lengthBytes, 0, 8)
} finally { $stream.Dispose() }
Write-Host "Created $output"
