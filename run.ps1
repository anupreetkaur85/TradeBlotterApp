#Requires -Version 5.1
<#
.SYNOPSIS
  Launches the Trade Blotter backend (.NET 9 API) and frontend (Vite dev server)
  together, each in its own PowerShell window.

.PARAMETER BackendPort
  Port for the API. Default 5080 (matches frontend/.env VITE_API_BASE_URL).

.PARAMETER FrontendPort
  Port for the Vite dev server. Default 5173 (in the API's CORS allowlist).

.PARAMETER OpenBrowser
  Open the web app in the default browser after launching.

.PARAMETER UseInMemory
  Run the API without SQL Server. Data resets when the API stops.

.EXAMPLE
  .\run.ps1 -OpenBrowser

.EXAMPLE
  .\run.ps1 -UseInMemory -OpenBrowser
#>
param(
    [int] $BackendPort = 5080,
    [int] $FrontendPort = 5173,
    [switch] $UseInMemory,
    [switch] $OpenBrowser
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

if (-not (Test-Path "$root\frontend\node_modules")) {
    Write-Host 'Installing frontend dependencies (first run)...' -ForegroundColor Yellow
    Push-Location "$root\frontend"
    npm install
    Pop-Location
}

# Backtick-escaped $env so the child shell (not this one) evaluates them.
$backendCmd = "Set-Location '$root\backend'; " +
    "`$env:ASPNETCORE_ENVIRONMENT = 'Development'; " +
    "`$env:ASPNETCORE_URLS = 'http://localhost:$BackendPort'; " +
    "`$env:Cors__AllowedOrigins__0 = 'http://localhost:$FrontendPort'; " +
    "`$env:Database__UseInMemory = '$($UseInMemory.IsPresent.ToString().ToLowerInvariant())'; " +
    "dotnet run --project TradeBlotter.Api --no-launch-profile"

$frontendCmd = "Set-Location '$root\frontend'; " +
    "`$env:VITE_API_BASE_URL = 'http://localhost:$BackendPort'; " +
    "npm run dev -- --port $FrontendPort"

Start-Process powershell -ArgumentList '-NoExit', '-Command', $backendCmd
Start-Process powershell -ArgumentList '-NoExit', '-Command', $frontendCmd

Write-Host ''
Write-Host 'Trade Blotter starting in two windows:' -ForegroundColor Green
Write-Host "  API      : http://localhost:$BackendPort"
Write-Host "  API docs : http://localhost:$BackendPort/api-docs  (root '/' redirects here)"
Write-Host "  Web app  : http://localhost:$FrontendPort"
Write-Host "  Storage  : $(if ($UseInMemory) { 'In-memory (resets on stop)' } else { 'SQL Server' })"
Write-Host ''
Write-Host 'Close those windows (or Ctrl+C in each) to stop.' -ForegroundColor DarkGray

if ($OpenBrowser) {
    Start-Process "http://localhost:$FrontendPort"
}
