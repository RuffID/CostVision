# syntax=docker/dockerfile:1.7

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

# Создание безопасного пользователя
RUN useradd -m -u 1001 appuser

# Создание рабочих директорий и прав доступа
RUN mkdir -p /app /app/Config /app/Logs /app/keys-linux && chown -R appuser:appuser /app

WORKDIR /app
EXPOSE 8080

# Этот этап используется для сборки проекта службы
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Временная строка для теста на какой платформе собирается сборка
RUN uname -m

ARG BUILD_CONFIGURATION=Release
WORKDIR /src
RUN apt-get update && apt-get install -y --no-install-recommends nodejs npm && rm -rf /var/lib/apt/lists/*
COPY ["CostVision/CostVision.Web.csproj", "CostVision/"]
COPY ["CostVision.Domain/CostVision.Domain.csproj", "CostVision.Domain/"]
COPY ["CostVision.Application/CostVision.Application.csproj", "CostVision.Application/"]
COPY ["CostVision.Infrastructure/CostVision.Infrastructure.csproj", "CostVision.Infrastructure/"]
RUN --mount=type=cache,target=/root/.nuget/packages dotnet restore "./CostVision/CostVision.Web.csproj" --disable-parallel
COPY ["CostVision/package.json", "CostVision/"]
COPY ["CostVision/package-lock.json", "CostVision/"]
WORKDIR "/src/CostVision"
RUN --mount=type=cache,target=/root/.npm npm ci
WORKDIR /src
COPY . .
WORKDIR "/src/CostVision"
RUN npm run build:ts

# Этот этап используется для публикации проекта службы, который будет скопирован на последний этап
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN --mount=type=cache,target=/root/.nuget/packages dotnet publish "./CostVision.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false /p:SkipFrontendBuild=true

# Этот этап используется в рабочей среде или при запуске из VS в обычном режиме (по умолчанию, когда конфигурация отладки не используется)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Переход на безопасного пользователя
USER appuser

ENTRYPOINT ["dotnet", "CostVision.Web.dll"]
