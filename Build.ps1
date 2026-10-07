[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GameBinPath,
    [Parameter(Mandatory = $true)][string]$PulsarRoot
)
$ErrorActionPreference = 'Stop'
$gameBin = (Resolve-Path -LiteralPath $GameBinPath).Path
$harmonyFile = (Resolve-Path -LiteralPath (Join-Path $PulsarRoot 'Libraries/Legacy/0Harmony.dll')).Path
$cecilFile = (Resolve-Path -LiteralPath (Join-Path $PulsarRoot 'Libraries/Legacy/Mono.Cecil.dll')).Path
if (-not (Test-Path -LiteralPath (Join-Path $gameBin 'VRage.Platform.Windows.dll'))) {
    throw 'GameBinPath must point to the Windows game Bin64 directory.'
}
# 공개 소스 빌드는 결과물만 만들고 사용자 설치 폴더를 변경하지 않는다.
#
# Build the public source without deploying to a user's installation.
& dotnet build (Join-Path $PSScriptRoot 'KoreanPatch.csproj') -c Release --nologo "-p:GameBinPath=$gameBin" "-p:HarmonyPath=$harmonyFile" "-p:CecilPath=$cecilFile"
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Assets/PluginVersion.xml')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets/PluginVersion.xml') -Destination (Join-Path $PSScriptRoot 'bin/Release/net48/PluginVersion.xml') -Force
}
