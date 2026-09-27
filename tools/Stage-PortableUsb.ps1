[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path $_ -PathType Container })]
    [string] $Destination
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceExe = Join-Path $projectRoot 'artifacts\usb-release\UsbLlm.exe'
$sourceModels = Join-Path $projectRoot 'Models'
if (-not (Test-Path $sourceExe -PathType Leaf)) { throw 'Build UsbLlm.exe before staging a USB drive.' }

$targetExe = Join-Path $Destination 'UsbLlm.exe'
$targetModels = Join-Path $Destination 'Models'
Copy-Item -LiteralPath $sourceExe -Destination $targetExe -Force
New-Item -ItemType Directory -Force -Path $targetModels | Out-Null
Get-ChildItem -LiteralPath $sourceModels -Force | Where-Object Name -ne '.cache' | Copy-Item -Destination $targetModels -Recurse -Force

Write-Host "Usb LLM was staged at $Destination. The chat app creates no history there."
