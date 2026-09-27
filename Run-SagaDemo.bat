@echo off
set ROOT=%~dp0
start "OrderService - 5001" cmd /k "cd /d "%ROOT%" && dotnet run --project "%ROOT%OrderService\OrderService.csproj""
start "InventoryService - 5002" cmd /k "cd /d "%ROOT%" && dotnet run --project "%ROOT%InventoryService\InventoryService.csproj""
start "PaymentService - 5003" cmd /k "cd /d "%ROOT%" && dotnet run --project "%ROOT%PaymentService\PaymentService.csproj""
start "SagaOrchestrator - 5004" cmd /k "cd /d "%ROOT%" && dotnet run --project "%ROOT%SagaOrchestrator\SagaOrchestrator.csproj""
