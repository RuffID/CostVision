FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

# Создание безопасного пользователя
RUN useradd -m -u 1001 appuser

# Создание рабочих директорий и прав доступа
RUN mkdir -p /app /app/Config /app/Logs /app/keys-linux && chown -R appuser:appuser /app

WORKDIR /app
EXPOSE 8080

# Этот этап используется для сборки проекта службы
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["CostVision/CostVision.csproj", "CostVision/"]
RUN dotnet restore "./CostVision/CostVision.csproj" --disable-parallel
COPY . .
WORKDIR "/src/CostVision"
RUN dotnet build "./CostVision.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Этот этап используется для публикации проекта службы, который будет скопирован на последний этап
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CostVision.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Этот этап используется в рабочей среде или при запуске из VS в обычном режиме (по умолчанию, когда конфигурация отладки не используется)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Переход на безопасного пользователя
USER appuser

ENTRYPOINT ["dotnet", "CostVision.dll"]