# MngSim - SNMP/HTTP monitoring simulator
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 6061 11161/udp 11162/udp 11163/udp 11164/udp
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["MngSim.csproj", "."]
RUN dotnet restore "MngSim.csproj"
COPY . .
RUN dotnet build "MngSim.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngSim.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngSim.dll"]
