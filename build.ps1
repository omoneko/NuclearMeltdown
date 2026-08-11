$ErrorActionPreference = "Stop"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found" }

& $msbuild "src\NuclearMeltdown\NuclearMeltdown.csproj" /t:Restore,Build /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$dll = "src\NuclearMeltdown\bin\Release\NuclearMeltdown.dll"
$modDir = Join-Path $env:LOCALAPPDATA "Colossal Order\Cities_Skylines\Addons\Mods\NuclearMeltdown"
New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-Item $dll $modDir -Force
$apiDll = "src\NuclearMeltdown\bin\Release\CitiesHarmony.API.dll"
if (Test-Path $apiDll) { Copy-Item $apiDll $modDir -Force }
# LocaleLoader reads Locales\<lang>.txt at runtime. en.txt is regenerated automatically when
# missing, but shipping it keeps the Workshop copy in step with the repo.
$localesSrc = "Locales"
if (Test-Path $localesSrc) {
    $localesDst = Join-Path $modDir "Locales"
    New-Item -ItemType Directory -Force -Path $localesDst | Out-Null
    Copy-Item (Join-Path $localesSrc "*") $localesDst -Include *.txt -Force
    Write-Host "Deployed locales"
}

Write-Host "Deploy complete: $modDir"
