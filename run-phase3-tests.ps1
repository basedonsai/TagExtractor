# PowerShell script to run Phase 3 tests
Write-Host "=======================================================
" -ForegroundColor Cyan
Write-Host "Phase 3 Checkpoint - Testing Structured Record Builders" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""

# Build the project with TestRunner as entry point
Write-Host "Building test runner..." -ForegroundColor Yellow
dotnet build /p:StartupObject=OCRTool.TestRunner /p:OutputType=Exe

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful. Running tests..." -ForegroundColor Green
    Write-Host ""
    
    # Run the compiled executable
    & ".\bin\Debug\net10.0-windows\win-x64\OCRTool.exe"
    
    Write-Host ""
    Write-Host "Tests completed." -ForegroundColor Green
} else {
    Write-Host "Build failed. Please check the errors above." -ForegroundColor Red
}
