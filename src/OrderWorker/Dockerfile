FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["OrderWorker.csproj", "OrderWorker/"]
RUN dotnet restore "OrderWorker/OrderWorker.csproj"

COPY . OrderWorker/
WORKDIR "/src/OrderWorker"
RUN dotnet build "OrderWorker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderWorker.dll"]