param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [switch] $SelfContained
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsRoot "SecParser-$Runtime"
$zipPath = Join-Path $artifactsRoot "SecParser-$Runtime.zip"
$checksumsPath = Join-Path $artifactsRoot "SHA256SUMS.txt"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null

dotnet restore (Join-Path $repoRoot "SecParser.slnx")
dotnet build (Join-Path $repoRoot "SecParser.slnx") --configuration $Configuration --no-restore
dotnet test (Join-Path $repoRoot "SecParser.slnx") --configuration $Configuration --no-build

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

dotnet publish (Join-Path $repoRoot "SecParser.UI\SecParser.UI.csproj") `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained $selfContainedValue `
    --output $publishDir `
    -p:PublishSingleFile=false

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath |
    ForEach-Object { "$($_.Hash)  $(Split-Path $_.Path -Leaf)" } |
    Set-Content -LiteralPath $checksumsPath

Write-Host "Release artifact: $zipPath"
Write-Host "Checksums: $checksumsPath"
