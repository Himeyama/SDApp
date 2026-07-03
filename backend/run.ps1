<#
.SYNOPSIS
    Pythonバックエンドを単体で起動する(デバッグ用)。
    通常はWinUI3アプリがこのバックエンドを自動起動するため、単体起動はAPI動作確認時のみ使う。

.PARAMETER Port
    リッスンするポート。既定は 8000。

.EXAMPLE
    ./run.ps1
    ./run.ps1 -Port 8001
#>
param(
    [int]$Port = 8000
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

uv sync
uv run sdapp-backend --port $Port
