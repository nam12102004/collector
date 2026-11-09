# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/attendanceCollector.MockDevice/attendanceCollector.MockDevice.csproj src/attendanceCollector.MockDevice/
RUN dotnet restore src/attendanceCollector.MockDevice/attendanceCollector.MockDevice.csproj

COPY src/ src/
WORKDIR /src/src/attendanceCollector.MockDevice
RUN dotnet publish attendanceCollector.MockDevice.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "attendanceCollector.MockDevice.dll"]
