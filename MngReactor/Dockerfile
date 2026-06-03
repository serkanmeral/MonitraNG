#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5003
# Healthcheck için curl
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

COPY . .
RUN dotnet restore "./Presentation/MngReactor.Api/MngReactor.Api.csproj"
RUN dotnet build "./Presentation/MngReactor.Api/MngReactor.Api.csproj" -c Release -o /app/build


FROM build AS publish
RUN dotnet publish "./Presentation/MngReactor.Api/MngReactor.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false


FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngReactor.Api.dll"]