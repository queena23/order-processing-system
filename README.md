# Real-Time Event-Driven Order Processing System

A production-style, cloud-native order processing backend built on Azure using C# and .NET 8.

## Architecture

```
POST /orders
     │
     ▼
┌─────────────────┐
│   OrderApi      │  Azure Function (HTTP trigger)
│                 │  • Validates input
│                 │  • Persists to Cosmos DB
│                 │  • Enqueues to Service Bus
└────────┬────────┘
         │ Service Bus: orders queue
         ▼
┌─────────────────┐
│  FraudChecker   │  Azure Function (Service Bus trigger)
│                 │  • Applies fraud rules
│                 │  • Dead-letters suspicious orders
│                 │  • Forwards clean orders onward
└────────┬────────┘
         │ Service Bus: orders-processing queue
         ▼
┌─────────────────┐
│  OrderWorker    │  Azure Container App (.NET Worker)
│                 │  • Processes approved orders
│                 │  • Marks orders as completed
└─────────────────┘
```

## Azure Services Used

| Service | Purpose |
|---|---|
| **Azure Functions** | HTTP API + FraudChecker (Service Bus trigger) |
| **Azure Service Bus** | Async decoupled messaging with dead-letter queues |
| **Azure Cosmos DB** | Order persistence (serverless, NoSQL) |
| **Azure Container Apps** | Containerized order processing worker |
| **Azure Container Registry** | Docker image storage |
| **Azure Bicep** | Infrastructure as Code |

## Tech Stack

- C# / .NET 8
- Azure Functions v4 (Isolated Worker)
- Azure Service Bus (Standard)
- Azure Cosmos DB (Serverless)
- Azure Container Apps
- Docker
- Bicep IaC
- GitHub Actions CI/CD

## Project Structure

```
src/
├── OrderApi/         # HTTP-triggered Azure Function
│   ├── SubmitOrder.cs
│   └── Order.cs
├── FraudChecker/     # Service Bus-triggered Azure Function
│   ├── FraudCheck.cs
│   └── Order.cs
└── OrderWorker/      # Containerized .NET Worker Service
    ├── Worker.cs
    └── Order.cs
infra/
└── main.bicep        # All Azure infrastructure defined as code
Dockerfile            # Multi-stage build for OrderWorker
```

## Key Design Decisions

**Why Service Bus instead of direct function calls?**
Decouples producers from consumers. If FraudChecker is slow or down, orders queue up safely. Service Bus handles retries and dead-lettering automatically.

**Why a Container App for OrderWorker instead of a Function?**
OrderWorker is a long-running background service that continuously listens to a queue. Container Apps with a minimum of 1 replica is a better fit than Functions which are designed for short-lived executions.

**Why dead-letter queues?**
Messages that fail fraud checks or exceed max delivery count are moved to a dead-letter queue instead of being lost. This allows investigation and replay of failed messages.

## Local Development

### Prerequisites
- .NET 8 SDK
- Azure Functions Core Tools v4
- Azurite (local storage emulator)
- Docker Desktop
- Azure CLI

### Setup

1. Clone the repo
2. Fill in `local.settings.json` in OrderApi and FraudChecker with your connection strings
3. Fill in `appsettings.Development.json` in OrderWorker

### Run locally

Open 4 terminals:

```powershell
# Terminal 1
azurite

# Terminal 2
cd src/OrderApi
func start

# Terminal 3
cd src/FraudChecker
func start --port 7072

# Terminal 4
cd src/OrderWorker
dotnet run
```

### Test

```powershell
Invoke-WebRequest -Uri http://localhost:7071/api/SubmitOrder `
  -Method POST `
  -ContentType "application/json" `
  -Body '{"customerId":"customer-123","items":[{"productName":"Book","quantity":2,"unitPrice":15.00}]}'
```

## Deployment

### Infrastructure (Bicep)

```powershell
az deployment group create --resource-group rg-order-processing --template-file infra/main.bicep --parameters environment=dev
```

### Functions

```powershell
cd src/OrderApi && func azure functionapp publish orders-dev-api-q7x2
cd src/FraudChecker && func azure functionapp publish orders-dev-fraud
```

### OrderWorker Container

```powershell
docker build -t order-worker .
docker tag order-worker <registry>.azurecr.io/order-worker:latest
docker push <registry>.azurecr.io/order-worker:latest
```

## Live API

```
POST https://orders-dev-api-q7x2.azurewebsites.net/api/SubmitOrder
```

## Cleanup

```powershell
az group delete --name rg-order-processing
```