# Build, test and package Character Design Prompt Generator for Windows x64.
# Usage:  powershell -ExecutionPolicy Bypass -File build.ps1 [-SkipTests] [-Output publish]
# Output: <Output>\CharacterPromptGenerator.exe  and  dist\CharacterPromptGenerator_v<version>_win-x64.zip
param([switch]$SkipTests, [string]$Output = 'publish')

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Invoke-Step([string]$Title, [scriptblock]$Command) {
    Write-Host "==> $Title" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Title failed (exit code $LASTEXITCODE)" }
}

$app = 'src\CharacterPromptGenerator.App\CharacterPromptGenerator.App.csproj'
$version = ([xml](Get-Content $app -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

Invoke-Step 'Restore' { dotnet restore CharacterPromptGenerator.slnx }
Invoke-Step 'Build (Release)' { dotnet build CharacterPromptGenerator.slnx -c Release --no-restore }
if (-not $SkipTests) {
    Invoke-Step 'Test' { dotnet test tests\CharacterPromptGenerator.Tests -c Release --no-build }
}

$outFull = if ([IO.Path]::IsPathRooted($Output)) { $Output } else { [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Output)) }
$running = Get-Process CharacterPromptGenerator -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($outFull, [StringComparison]::OrdinalIgnoreCase) }
if ($running) { throw "CharacterPromptGenerator.exe is running from $outFull. Close it first, or use -Output <other folder>." }
if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
Invoke-Step 'Publish (win-x64, self-contained, single file)' {
    dotnet publish $app -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none -o $Output
}

# Ship the docs and sample import files next to the EXE
Copy-Item README.md $Output
Copy-Item samples (Join-Path $Output 'samples') -Recurse

New-Item -ItemType Directory -Force dist | Out-Null
$zip = "dist\CharacterPromptGenerator_v${version}_win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $Output '*') -DestinationPath $zip

Write-Host ''
Write-Host "Done. EXE: $Output\CharacterPromptGenerator.exe" -ForegroundColor Green
Write-Host "      ZIP: $zip" -ForegroundColor Green
