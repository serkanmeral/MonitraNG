#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.
# Build context: repo root (see docker-compose.production.yml mngengine.build.context)

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5037
EXPOSE 5514/udp
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY MngEngine/MngEngine.Service/ ./MngEngine/MngEngine.Service/
WORKDIR /src/MngEngine/MngEngine.Service
RUN dotnet restore "./Presentation/MngEngine.Api/MngEngine.Api.csproj"
RUN dotnet build "./Presentation/MngEngine.Api/MngEngine.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./Presentation/MngEngine.Api/MngEngine.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY tests/fixtures/siem/ /app/fixtures/siem/
RUN mkdir -p /app/persist
ENTRYPOINT ["dotnet", "MngEngine.Api.dll"]
