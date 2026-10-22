# Build (and optionally install) the Android app of Character Design Prompt Generator.
# Usage:  powershell -ExecutionPolicy Bypass -File build-android.ps1 [-Configuration Release] [-SkipTests] [-Install]
# Output: dist\CharacterPromptGenerator_v<version>_android.apk  (Release only; signed with the debug key unless
#         AndroidSigningKeyStore etc. are passed through -SigningArgs)
# Needs:  dotnet workload maui-android, Microsoft OpenJDK 21, Android SDK (see README).
# The Windows build (build.ps1) is not affected and does not need the MAUI workload.
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$Install,
    [string]$JavaSdk = $env:CPG_JAVA_SDK,
    [string]$AndroidSdk = $env:CPG_ANDROID_SDK,
    [string[]]$SigningArgs = @()
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Invoke-Step([string]$Title, [scriptblock]$Command) {
    Write-Host "==> $Title" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Title failed (exit code $LASTEXITCODE)" }
}

if (-not $JavaSdk) {
    $JavaSdk = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\OpenJDK" -Directory -Filter 'jdk-21*' -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $JavaSdk -or -not (Test-Path "$JavaSdk\bin\javac.exe")) { throw 'OpenJDK 21 not found. Pass -JavaSdk <path> or set CPG_JAVA_SDK.' }
if (-not $AndroidSdk) { $AndroidSdk = 'C:\Program Files (x86)\Android\android-sdk' }
if (-not (Test-Path "$AndroidSdk\platform-tools")) { throw 'Android SDK not found. Pass -AndroidSdk <path> or set CPG_ANDROID_SDK.' }

# aapt2 cannot handle non-ASCII project paths (error APT2265)
if ($PSScriptRoot -match '[^\x00-\x7F]') { throw "The project path contains non-ASCII characters, which the Android tools cannot handle: $PSScriptRoot" }

$project = 'src\CharacterPromptGenerator.Mobile\CharacterPromptGenerator.Mobile.csproj'
$common = @("-p:JavaSdkDirectory=$JavaSdk", "-p:AndroidSdkDirectory=$AndroidSdk", '-f', 'net10.0-android', '-c', $Configuration)

if (-not $SkipTests) {
    Invoke-Step 'Test (shared Core)' { dotnet test tests\CharacterPromptGenerator.Tests -c Release }
}

$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.ApplicationDisplayVersion | Where-Object { $_ } | Select-Object -First 1
$out = "src\CharacterPromptGenerator.Mobile\bin\$Configuration\net10.0-android"

if ($Configuration -eq 'Release') {
    Invoke-Step 'Publish APK (Release)' { dotnet publish $project @common -p:AndroidPackageFormats=apk @SigningArgs }
    $apk = Get-ChildItem "$out\publish\*-Signed.apk" | Select-Object -First 1
} else {
    # Debug normally uses Fast Deployment (assemblies are not inside the APK), so a standalone install would crash
    Invoke-Step 'Build APK (Debug)' { dotnet build $project @common -p:EmbedAssembliesIntoApk=true }
    $apk = Get-ChildItem "$out\*-Signed.apk" | Select-Object -First 1
}
if (-not $apk) { throw "No signed APK found under $out" }

New-Item -ItemType Directory -Force dist | Out-Null
$suffix = if ($Configuration -eq 'Release') { '' } else { '_debug' }
$target = "dist\CharacterPromptGenerator_v${version}_android$suffix.apk"
Copy-Item $apk.FullName $target -Force
Write-Host ''
Write-Host ("APK: {0} ({1:N1} MB)" -f $target, ((Get-Item $target).Length / 1MB)) -ForegroundColor Green

if ($Install) {
    # -r keeps the app's data (word bank, presets, history) when updating
    Invoke-Step 'Install on device' { & "$AndroidSdk\platform-tools\adb.exe" install -r $target }
}
