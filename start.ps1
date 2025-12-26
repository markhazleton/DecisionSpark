#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the DecisionSpark web application and opens it in a browser.

.DESCRIPTION
    This script starts the ASP.NET Core DecisionSpark application and automatically
    opens the default browser to the application URL.

.PARAMETER UseHttps
    Use HTTPS instead of HTTP (default: HTTP)

.PARAMETER Port
    Specify a custom port (default: 5000 for HTTP, 5001 for HTTPS)

.EXAMPLE
    .\start.ps1
    Starts the app on http://localhost:5000

.EXAMPLE
    .\start.ps1 -UseHttps
    Starts the app on https://localhost:5001

.EXAMPLE
    .\start.ps1 -Port 8080
    Starts the app on http://localhost:8080
#>

param(
    [switch]$UseHttps,
    [int]$Port
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Colors for output
$SuccessColor = "Green"
$InfoColor = "Cyan"
$ErrorColor = "Red"
$WarningColor = "Yellow"

# Determine project directory
$projectDir = Join-Path $PSScriptRoot "DecisionSpark"

# Check if project exists
if (-not (Test-Path $projectDir)) {
    Write-Host "ERROR: Project directory not found at: $projectDir" -ForegroundColor $ErrorColor
    exit 1
}

# Determine URL
if ($UseHttps) {
    $defaultPort = if ($Port) { $Port } else { 5001 }
    $url = "https://localhost:$defaultPort"
    $protocol = "https"
} else {
    $defaultPort = if ($Port) { $Port } else { 5000 }
    $url = "http://localhost:$defaultPort"
    $protocol = "http"
}

Write-Host "`n========================================" -ForegroundColor $InfoColor
Write-Host "  Starting DecisionSpark" -ForegroundColor $InfoColor
Write-Host "========================================" -ForegroundColor $InfoColor
Write-Host "URL: " -NoNewline -ForegroundColor $InfoColor
Write-Host $url -ForegroundColor $SuccessColor
Write-Host "Project: " -NoNewline -ForegroundColor $InfoColor
Write-Host $projectDir -ForegroundColor $SuccessColor
Write-Host "========================================`n" -ForegroundColor $InfoColor

# Change to project directory
Set-Location $projectDir

Write-Host "Cleaning project..." -ForegroundColor $InfoColor

# Clean the project to remove all build artifacts
$cleanResult = dotnet clean --configuration Release 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nClean failed!" -ForegroundColor $ErrorColor
    Write-Host $cleanResult -ForegroundColor $ErrorColor
    exit 1
}

Write-Host "Clean successful!`n" -ForegroundColor $SuccessColor

Write-Host "Restoring packages..." -ForegroundColor $InfoColor

# Restore NuGet packages
$restoreResult = dotnet restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nRestore failed!" -ForegroundColor $ErrorColor
    Write-Host $restoreResult -ForegroundColor $ErrorColor
    exit 1
}

Write-Host "Restore successful!`n" -ForegroundColor $SuccessColor

Write-Host "Building project..." -ForegroundColor $InfoColor

# Build the project fresh to catch any compilation errors
$buildResult = dotnet build --configuration Release --no-restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBuild failed!" -ForegroundColor $ErrorColor
    Write-Host $buildResult -ForegroundColor $ErrorColor
    exit 1
}

Write-Host "Build successful!`n" -ForegroundColor $SuccessColor

# Start the application in the background
Write-Host "Starting application..." -ForegroundColor $InfoColor
Write-Host "Press Ctrl+C to stop the server`n" -ForegroundColor $WarningColor

# Create a job to run dotnet
$job = Start-Job -ScriptBlock {
    param($dir, $url)
    Set-Location $dir
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    dotnet run --no-build --configuration Release --urls $url
} -ArgumentList $projectDir, $url

# Wait a bit for the server to start
Write-Host "Waiting for server to start..." -ForegroundColor $InfoColor
Start-Sleep -Seconds 3

# Check if the job is still running
if ($job.State -ne "Running") {
    Write-Host "`nApplication failed to start!" -ForegroundColor $ErrorColor
    Receive-Job -Job $job
    Remove-Job -Job $job -Force
    exit 1
}

# Open browser
Write-Host "Opening browser..." -ForegroundColor $InfoColor
Start-Process $url

Write-Host "`n" -ForegroundColor $SuccessColor
Write-Host "========================================" -ForegroundColor $SuccessColor
Write-Host "  DecisionSpark is running!" -ForegroundColor $SuccessColor
Write-Host "========================================" -ForegroundColor $SuccessColor
Write-Host "URL: $url" -ForegroundColor $SuccessColor
Write-Host "Press Ctrl+C to stop the server" -ForegroundColor $WarningColor
Write-Host "========================================`n" -ForegroundColor $SuccessColor

# Monitor the job and display output
try {
    while ($true) {
        # Check if job is still running
        if ($job.State -ne "Running") {
            Write-Host "`nApplication stopped unexpectedly!" -ForegroundColor $ErrorColor
            break
        }
        
        # Receive any new output from the job
        $output = Receive-Job -Job $job
        if ($output) {
            Write-Host $output
        }
        
        Start-Sleep -Milliseconds 500
    }
} finally {
    # Cleanup
    Write-Host "`nStopping application..." -ForegroundColor $WarningColor
    Stop-Job -Job $job -ErrorAction SilentlyContinue
    Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    Write-Host "Application stopped." -ForegroundColor $InfoColor
}
