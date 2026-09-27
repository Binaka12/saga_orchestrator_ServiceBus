# Saga Pattern Demo - .NET 10

A runnable four-service ASP.NET Core solution based on the Saga Orchestration example supplied by the user.

## Solution

- OrderService - http://localhost:5001
- InventoryService - http://localhost:5002
- PaymentService - http://localhost:5003
- SagaOrchestrator - http://localhost:5004

No Docker, RabbitMQ, database, or external infrastructure is required.

## Requirements

- Visual Studio 2026 (or a .NET 10 compatible Visual Studio version)
- .NET 10 SDK

## Run in Visual Studio

1. Open `SagaDemoNet10.sln`.
2. Right-click the solution -> **Set Startup Projects...**.
3. Select **Multiple startup projects**.
4. Set **Start** for all four projects.
5. Press F5.

Each service uses a fixed HTTP port so the Saga Orchestrator can call the other services.

## Test the Saga

POST:
`http://localhost:5004/api/saga`

Body:
```json
{
  "productId": "Product1",
  "quantity": 10
}
```

Expected successful response:
```json
{
  "id": 1,
  "productId": "Product1",
  "quantity": 10,
  "status": "Completed"
}
```

## Useful endpoints

Order:
- POST `http://localhost:5001/api/orders`
- GET `http://localhost:5001/api/orders/1`
- DELETE `http://localhost:5001/api/orders/1`

Inventory:
- GET `http://localhost:5002/api/inventory`
- POST `http://localhost:5002/api/inventory/reserve`
- POST `http://localhost:5002/api/inventory/release`

Payment:
- GET `http://localhost:5003/api/payments`
- POST `http://localhost:5003/api/payments`

Saga:
- POST `http://localhost:5004/api/saga`

## Important note

The original article uses `dynamic` request objects. This sample uses strongly typed request DTOs instead, which is safer and easier to debug while preserving the same Saga flow.

## One-command run

If you have the .NET 10 SDK installed, from the extracted folder you can run:

```powershell
.\Run-SagaDemo.ps1
```

Or double-click `Run-SagaDemo.bat` on Windows.

## Visual Studio HTTP testing

The solution also includes `SagaDemo.http`. Open it in Visual Studio and send the requests directly from the HTTP editor.
