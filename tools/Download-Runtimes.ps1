[CmdletBinding()]
param(
    [string] $Destination = (Join-Path $PSScriptRoot '..\Runtime')
)

$ErrorActionPreference = 'Stop'
$releases = Invoke-RestMethod -Uri 'https://api.github.com/repos/ggml-org/llama.cpp/releases?per_page=10' -Headers @{ 'User-Agent' = 'UsbLLM-build' }
$release = $releases | Where-Object {
    $_.assets.name -contains "llama-$($_.tag_name)-bin-win-cpu-x64.zip" -and
    $_.assets.name -contains "llama-$($_.tag_name)-bin-win-vulkan-x64.zip" -and
    $_.assets.name -contains "llama-$($_.tag_name)-bin-win-cuda-12.4-x64.zip"
} | Select-Object -First 1
if ($null -eq $release) { throw 'No current llama.cpp release contains all required Windows x64 backend archives.' }
$assets = @{}
foreach ($asset in $release.assets) { $assets[$asset.name] = $asset.browser_download_url }

$required = @{
    'CPU' = "llama-$($release.tag_name)-bin-win-cpu-x64.zip"
    'Vulkan' = "llama-$($release.tag_name)-bin-win-vulkan-x64.zip"
    'CUDA' = "llama-$($release.tag_name)-bin-win-cuda-12.4-x64.zip"
    'CUDA-runtime' = "cudart-llama-bin-win-cuda-12.4-x64.zip"
}

foreach ($entry in $required.GetEnumerator()) {
    if (-not $assets.ContainsKey($entry.Value)) { throw "Release $($release.tag_name) does not contain $($entry.Value)." }
    $target = if ($entry.Key -eq 'CUDA-runtime') { Join-Path $Destination 'CUDA' } else { Join-Path $Destination $entry.Key }
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    $archive = Join-Path $env:TEMP $entry.Value
    Invoke-WebRequest -Uri $assets[$entry.Value] -OutFile $archive
    Expand-Archive -Path $archive -DestinationPath $target -Force
    Remove-Item -LiteralPath $archive -Force
}

Write-Host "Installed llama.cpp $($release.tag_name) into $Destination."
