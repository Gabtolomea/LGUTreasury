FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080 8443
ENV ASPNETCORE_URLS="http://+:8080;https://+:8443"
ENV ASPNETCORE_HTTPS_PORT=8443
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=5 CMD curl --fail --silent --insecure https://localhost:8443/health || exit 1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["LGUTreasury.csproj", "."]
RUN dotnet restore "LGUTreasury.csproj"
COPY . .
RUN dotnet publish "LGUTreasury.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LGUTreasury.dll"]
