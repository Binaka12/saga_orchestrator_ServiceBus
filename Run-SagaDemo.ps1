$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Starting Saga Demo services..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList '-NoExit','-Command',"Set-Location '$root'; dotnet run --project '$root\OrderService\OrderService.csproj'"
Start-Process powershell -ArgumentList '-NoExit','-Command',"Set-Location '$root'; dotnet run --project '$root\InventoryService\InventoryService.csproj'"
Start-Process powershell -ArgumentList '-NoExit','-Command',"Set-Location '$root'; dotnet run --project '$root\PaymentService\PaymentService.csproj'"
Start-Process powershell -ArgumentList '-NoExit','-Command',"Set-Location '$root'; dotnet run --project '$root\SagaOrchestrator\SagaOrchestrator.csproj'"

Write-Host "Services are starting on ports 5001, 5002, 5003 and 5004." -ForegroundColor Green
Write-Host "Saga endpoint: http://localhost:5004/api/saga" -ForegroundColor Green
