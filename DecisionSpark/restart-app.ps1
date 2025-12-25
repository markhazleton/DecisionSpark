# Restart DecisionSpark Application
# This script stops any running instance and starts the app fresh

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  DecisionSpark Application Restart Script" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Set location
$projectPath = "C:\GitHub\MarkHazleton\DecisionSpark\DecisionSpark"
Set-Location $projectPath

Write-Host "1. Stopping any running DecisionSpark processes..." -ForegroundColor Yellow
# Kill any running dotnet process for DecisionSpark
Get-Process -Name "DecisionSpark" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | 
    Where-Object { $_.CommandLine -like "*DecisionSpark*" } | 
    Stop-Process -Force
Write-Host "   ? Processes stopped" -ForegroundColor Green
Write-Host ""

Write-Host "2. Verifying build is up to date..." -ForegroundColor Yellow
dotnet build --no-restore > $null 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ? Build successful" -ForegroundColor Green
} else {
    Write-Host "   ? Build failed!" -ForegroundColor Red
    Write-Host "   Run 'dotnet build' to see errors" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "3. Checking OpenAI configuration..." -ForegroundColor Yellow
$secrets = dotnet user-secrets list 2>&1
if ($secrets -like "*OpenAI:ApiKey*") {
    $apiKey = ($secrets | Select-String "OpenAI:ApiKey = (.+)" -AllMatches).Matches.Groups[1].Value
    if ($apiKey -like "sk-*") {
        Write-Host "   ? OpenAI API key configured" -ForegroundColor Green
    } else {
        Write-Host "   ? OpenAI API key may be invalid" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ? OpenAI not configured - will use fallback mode" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "4. Starting DecisionSpark application..." -ForegroundColor Yellow
Write-Host "   App will start at: https://localhost:44356" -ForegroundColor Cyan
Write-Host "   Press Ctrl+C to stop" -ForegroundColor Gray
Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Start the application
dotnet run

Write-Host ""
Write-Host "Application stopped." -ForegroundColor Yellow
