[CmdletBinding()]
param(
    [string]$GamePath = $env:STARDEW_GAME_PATH,
    [string[]]$CompatibilityModPaths = @(),
    [switch]$PrepareOnly,
    [ValidateRange(30, 600)][int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
function Read-ModManifest([string]$Path) {
    $text = [IO.File]::ReadAllText($Path)
    # SMAPI permits JSON comments and trailing commas; preserve quoted strings.
    $quoted = '"(?:\\.|[^"\\])*"'
    $text = [regex]::Replace($text, ('(' + $quoted + ')|/\*[\s\S]*?\*/|//[^\r\n]*'), {
        param($match)
        if ($match.Groups[1].Success) { $match.Value } else { ' ' }
    })
    $text = [regex]::Replace($text, ('(' + $quoted + ')|,(?=\s*[}\]])'), {
        param($match)
        if ($match.Groups[1].Success) { $match.Value } else { '' }
    })
    return $text | ConvertFrom-Json
}
if ([string]::IsNullOrWhiteSpace($GamePath)) {
    throw 'Pass -GamePath or set STARDEW_GAME_PATH to your game installation.'
}
$game = (Resolve-Path -LiteralPath $GamePath).ProviderPath
if (!(Test-Path -LiteralPath (Join-Path $game 'StardewModdingAPI.exe'))) {
    throw 'This native test runner requires the Windows version of SMAPI.'
}
if (!$PrepareOnly -and (Get-Process -Name StardewModdingAPI, 'Stardew Valley' -ErrorAction SilentlyContinue)) {
    throw 'Save and exit the running game before starting the native fixture, or use -PrepareOnly.'
}
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source = Join-Path $PSScriptRoot 'src'
$tests = Join-Path $PSScriptRoot 'tests/native'
& dotnet build (Join-Path $tests 'BehaviorProbe.csproj') -c Release --nologo "-p:GamePath=$game"
if ($LASTEXITCODE -ne 0) { throw 'Native fixture build failed.' }

$run = Join-Path $repo ('.artifacts/BehaviorAutomation/native/' + [Guid]::NewGuid().ToString('N'))
$runtime = Join-Path $run 'mods'
$product = Join-Path $runtime 'BehaviorAutomation'
$probe = Join-Path $runtime 'BehaviorProbe'
$evidence = Join-Path $run 'evidence'
New-Item -ItemType Directory -Force -Path $product, $probe, $evidence | Out-Null
Copy-Item -LiteralPath (Join-Path $source 'bin/Release/net6.0/BehaviorAutomation.dll') -Destination $product
Copy-Item -LiteralPath (Join-Path $source 'manifest.json') -Destination $product
Copy-Item -LiteralPath (Join-Path $source 'i18n') -Destination $product -Recurse
Copy-Item -LiteralPath (Join-Path $tests 'bin/Release/net6.0/BehaviorProbe.dll') -Destination $probe

$supported = @('spacechase0.GenericModConfigMenu', 'NCarigon.PassableCrops', 'sznine.SmartWateringCan', 'bcmpinc.StardewHack', 'bcmpinc.HarvestWithScythe')
$loaded = @()
foreach ($path in $CompatibilityModPaths) {
    $origin = (Resolve-Path -LiteralPath $path).ProviderPath
    $manifest = Read-ModManifest (Join-Path $origin 'manifest.json')
    $id = [string]$manifest.UniqueID
    if ($id -notin $supported -or $id -in $loaded) {
        throw "Unsupported or duplicate compatibility fixture: $id"
    }
    $target = Join-Path $runtime $id
    Copy-Item -LiteralPath $origin -Destination $target -Recurse
    $loaded += $id
    if ($id -eq 'NCarigon.PassableCrops') {
        @{
            PassableCrops = $false; PassableScarecrows = $false; PassableSprinklers = $false
            PassableForage = $false; PassableTeaBushes = $false; PassableWeeds = $false
            PassableTreeGrowth = 0; PassableFruitTreeGrowth = 0
            SlowDownWhenPassing = $true; ShakeWhenPassing = $true; PlaySoundWhenPassing = $true
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $target 'config.json') -Encoding UTF8
    }
}

@{
    Name = 'Behavior native fixture'; Author = 'sznine'; Version = '1.0.0'
    UniqueID = 'sznine.BehaviorProbe'; EntryDll = 'BehaviorProbe.dll'; MinimumApiVersion = '4.5.2'
    Dependencies = @(@{ UniqueID = 'sznine.BehaviorAutomation'; IsRequired = $true })
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $probe 'manifest.json') -Encoding UTF8

if ($PrepareOnly) {
    [pscustomobject]@{ Prepared = $true; RuntimeMods = $runtime; Evidence = $evidence; CompatibilityMods = $loaded }
    return
}

$process = Start-Process (Join-Path $game 'StardewModdingAPI.exe') `
    -ArgumentList ('--mods-path "' + $runtime + '"') -WorkingDirectory $game -WindowStyle Hidden `
    -RedirectStandardOutput (Join-Path $evidence 'run.txt') `
    -RedirectStandardError (Join-Path $evidence 'run-error.txt') -PassThru
if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    throw "Native fixture timed out. Inspect $evidence"
}
$resultFile = Join-Path $evidence 'native-result.json'
if (!(Test-Path -LiteralPath $resultFile)) {
    throw "Native fixture exited without a result. Inspect $evidence"
}
$result = Get-Content -LiteralPath $resultFile -Raw -Encoding UTF8 | ConvertFrom-Json
$expectedHash = (Get-FileHash -LiteralPath (Join-Path $product 'BehaviorAutomation.dll') -Algorithm SHA256).Hash
if ($result.ProductSHA256 -ne $expectedHash -or $result.Failed -ne 0 -or $result.Passed -le 0) {
    throw "Native fixture failed or tested a different DLL. Inspect $resultFile"
}
$result
Write-Output "Evidence: $evidence"
