# multi-stage build for .NET 10
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5004
EXPOSE 5004

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files required for restore (include shared Contracts and any referenced projects)
COPY ["SagaOrchestrator/SagaOrchestrator.csproj", "SagaOrchestrator/"]
COPY ["Contracts/Contracts.csproj", "Contracts/"]
COPY ["OrderService/OrderService.csproj", "OrderService/"]
COPY ["InventoryService/InventoryService.csproj", "InventoryService/"]
COPY ["PaymentService/PaymentService.csproj", "PaymentService/"]

# Restore (restore the orchestrator project which references Contracts)
RUN dotnet restore "SagaOrchestrator/SagaOrchestrator.csproj"

# Copy all sources and build
COPY . .
WORKDIR "/src/SagaOrchestrator"
RUN dotnet build "SagaOrchestrator.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SagaOrchestrator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SagaOrchestrator.dll"]