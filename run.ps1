<#
.SYNOPSIS
    SDApp (WinUI3フロントエンド) をビルドして起動する。
    Pythonバックエンドはフロントエンドが自動的にサブプロセスとして起動する。

.PARAMETER Configuration
    ビルド構成。既定は Debug。

.EXAMPLE
    ./run.ps1
    ./run.ps1 -Configuration Release
#>
param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$frontendProject = Join-Path $root "frontend\SDApp"
$exePath = Join-Path $frontendProject "bin\$Configuration\net9.0-windows10.0.26100.0\win-x64\SDApp.exe"

Write-Host "==> uv sync (backend)" -ForegroundColor Cyan
Push-Location (Join-Path $root "backend")
try {
    uv sync
}
finally {
    Pop-Location
}

Write-Host "==> dotnet build (frontend, $Configuration)" -ForegroundColor Cyan
Push-Location $frontendProject
try {
    dotnet build -c $Configuration
}
finally {
    Pop-Location
}

if (-not (Test-Path $exePath)) {
    throw "Build succeeded but executable not found at: $exePath"
}

Write-Host "==> Launching SDApp" -ForegroundColor Cyan
Write-Host "    (Python backend starts automatically as a child process; closing the window stops it too.)"
& $exePath
