[CmdletBinding()]
param([string]$GamePath = $env:STARDEW_GAME_PATH)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GamePath)) {
    throw 'Pass -GamePath or set STARDEW_GAME_PATH to your game installation.'
}
$game = (Resolve-Path -LiteralPath $GamePath).ProviderPath
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source = Join-Path $PSScriptRoot 'src'

& dotnet build (Join-Path $source 'BehaviorAutomation.csproj') -c Release --nologo "-p:GamePath=$game"
if ($LASTEXITCODE -ne 0) { throw 'BehaviorAutomation build failed.' }

$output = Join-Path $repo '.artifacts/BehaviorAutomation/package'
$product = Join-Path $output 'BehaviorAutomation'
New-Item -ItemType Directory -Force -Path (Join-Path $product 'i18n') | Out-Null
Copy-Item -LiteralPath (Join-Path $source 'bin/Release/net6.0/BehaviorAutomation.dll') -Destination $product -Force
Copy-Item -LiteralPath (Join-Path $source 'manifest.json') -Destination $product -Force
foreach ($language in @('default', 'zh')) {
    Copy-Item -LiteralPath (Join-Path $source "i18n/$language.json") -Destination (Join-Path $product 'i18n') -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $product -Force
$guide = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'docs/USER-GUIDE.zh-CN.md'))
$guide = [regex]::Replace($guide, '(?m)^!\[.*?\]\(.*?\)\r?\n', '')
[IO.File]::WriteAllText((Join-Path $product '使用说明.md'), $guide, [Text.UTF8Encoding]::new($false))

$manifest = Get-Content -LiteralPath (Join-Path $source 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$zip = Join-Path $output ("BehaviorAutomation-{0}.zip" -f $manifest.Version)
$files = @('BehaviorAutomation.dll', 'manifest.json', 'i18n/default.json', 'i18n/zh.json', '使用说明.md', 'LICENSE')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [IO.File]::Open($zip, [IO.FileMode]::Create)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, (Join-Path $product $file), "BehaviorAutomation/$file",
            [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
    $stream.Dispose()
}

[pscustomobject]@{
    Version = $manifest.Version
    Package = $zip
    SHA256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
}
