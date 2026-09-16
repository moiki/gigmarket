FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["services/orders-api/Orders.Api/Orders.Api.csproj", "services/orders-api/Orders.Api/"]
COPY ["services/orders-api/Orders.Application/Orders.Application.csproj", "services/orders-api/Orders.Application/"]
COPY ["services/orders-api/Orders.Domain/Orders.Domain.csproj", "services/orders-api/Orders.Domain/"]
COPY ["services/orders-api/Orders.Infrastructure/Orders.Infrastructure.csproj", "services/orders-api/Orders.Infrastructure/"]
COPY ["libs/BuildingBlocks.Common/BuildingBlocks.Common.csproj", "libs/BuildingBlocks.Common/"]
COPY ["libs/BuildingBlocks.AspNetCore/BuildingBlocks.AspNetCore.csproj", "libs/BuildingBlocks.AspNetCore/"]
RUN dotnet restore "services/orders-api/Orders.Api/Orders.Api.csproj"

COPY . .
RUN dotnet publish "services/orders-api/Orders.Api/Orders.Api.csproj" \
    -c "$BUILD_CONFIGURATION" \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
USER app
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
    CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Orders.Api.dll"]
