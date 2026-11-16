param(
    [switch]$SelfContained,
    [switch]$Installer
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

if (Get-Process RightDock -ErrorAction SilentlyContinue) {
    throw "Close RightDock before publishing; the published files are locked while it runs."
}

[xml]$project = Get-Content "RightDock.csproj"
$version = $project.Project.PropertyGroup.Version
$self = if ($SelfContained) { "true" } else { "false" }

Write-Output "Publishing RightDock $version (win-x64, self-contained=$self)..."
& dotnet publish "RightDock.csproj" -c Release -r win-x64 --self-contained $self -o publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

if (-not (Test-Path "dist")) { New-Item -ItemType Directory -Path "dist" | Out-Null }
$zip = "dist\RightDock-$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "publish\*" -DestinationPath $zip -CompressionLevel Optimal
Write-Output "Portable package: $zip"

if ($Installer) {
    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) {
        throw "Inno Setup 6 was not found. Install it from https://jrsoftware.org/isinfo.php and rerun with -Installer."
    }

    & $iscc "installer\RightDock.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed." }
    Write-Output "Installer: dist\RightDock-$version-setup.exe"
}