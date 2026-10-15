# syntax=docker/dockerfile:1.7

FROM --platform=$BUILDPLATFORM node:22.18.0-bookworm-slim AS frontend-toolchain

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS base

# Создание безопасного пользователя
RUN useradd -m -u 1001 appuser

# Создание рабочих директорий и прав доступа
RUN mkdir -p /app /app/Config /app/Logs /app/keys-linux && chown -R appuser:appuser /app

WORKDIR /app
EXPOSE 8080

# Этот этап используется для сборки проекта службы
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
COPY --from=frontend-toolchain /usr/local/ /usr/local/

ARG BUILD_CONFIGURATION=Release
WORKDIR /src
RUN test "$(node --version)" = "v22.18.0" && test "$(npm --version)" = "10.9.3"
COPY ["global.json", "NuGet.Config", "./"]
COPY ["libs/", "libs/"]
COPY ["CostVision.Web/CostVision.Web.csproj", "CostVision.Web/"]
COPY ["CostVision.Domain/CostVision.Domain.csproj", "CostVision.Domain/"]
COPY ["CostVision.Application/CostVision.Application.csproj", "CostVision.Application/"]
COPY ["CostVision.Infrastructure/CostVision.Infrastructure.csproj", "CostVision.Infrastructure/"]
RUN --mount=type=cache,target=/root/.nuget/packages dotnet restore "./CostVision.Web/CostVision.Web.csproj" --configfile /src/NuGet.Config --disable-parallel -p:SkipFrontendBuild=true
COPY ["CostVision.Web/package.json", "CostVision.Web/"]
COPY ["CostVision.Web/package-lock.json", "CostVision.Web/"]
COPY ["CostVision.Web/.npmrc", "CostVision.Web/"]
WORKDIR "/src/CostVision.Web"
RUN --mount=type=cache,target=/root/.npm npm ci --ignore-scripts --no-audit --no-fund
WORKDIR /src
COPY . .
WORKDIR "/src/CostVision.Web"
RUN npm run build:ts

# Этот этап используется для публикации проекта службы, который будет скопирован на последний этап
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN --mount=type=cache,target=/root/.nuget/packages dotnet publish "./CostVision.Web.csproj" --no-restore -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:SkipFrontendBuild=true

# Этот этап используется в рабочей среде или при запуске из VS в обычном режиме (по умолчанию, когда конфигурация отладки не используется)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Переход на безопасного пользователя
USER appuser

ENTRYPOINT ["dotnet", "CostVision.Web.dll"]
