# syntax=docker/dockerfile:1
FROM node:24-alpine AS web-build
WORKDIR /web
COPY src/attendanceCollector.WebApp/package.json src/attendanceCollector.WebApp/package-lock.json ./
RUN npm ci
COPY src/attendanceCollector.WebApp/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/attendanceCollector.Domain/attendanceCollector.Domain.csproj src/attendanceCollector.Domain/
COPY src/attendanceCollector.Application/attendanceCollector.Application.csproj src/attendanceCollector.Application/
COPY src/attendanceCollector.Infrastructure/attendanceCollector.Infrastructure.csproj src/attendanceCollector.Infrastructure/
COPY src/attendanceCollector.API/attendanceCollector.API.csproj src/attendanceCollector.API/
RUN dotnet restore src/attendanceCollector.API/attendanceCollector.API.csproj

COPY src/ src/
COPY --from=web-build /web/dist/ /src/src/attendanceCollector.API/wwwroot/
WORKDIR /src/src/attendanceCollector.API
RUN dotnet publish attendanceCollector.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
RUN mkdir -p /var/lib/collector/keys && chown "$APP_UID:$APP_UID" /var/lib/collector/keys && chmod 700 /var/lib/collector/keys
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "attendanceCollector.API.dll"]

