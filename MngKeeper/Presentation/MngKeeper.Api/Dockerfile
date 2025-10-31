FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5001

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Presentation/MngKeeper.Api/MngKeeper.Api.csproj", "Presentation/MngKeeper.Api/"]
COPY ["Core/MngKeeper.Application/MngKeeper.Application.csproj", "Core/MngKeeper.Application/"]
COPY ["Core/MngKeeper.Domain/MngKeeper.Domain.csproj", "Core/MngKeeper.Domain/"]
COPY ["Infrastructure/MngKeeper.Infrastructure/MngKeeper.Infrastructure.csproj", "Infrastructure/MngKeeper.Infrastructure/"]
COPY ["Infrastructure/MngKeeper.Persistence/MngKeeper.Persistence.csproj", "Infrastructure/MngKeeper.Persistence/"]
RUN dotnet restore "Presentation/MngKeeper.Api/MngKeeper.Api.csproj"
COPY . .
WORKDIR "/src/Presentation/MngKeeper.Api"
RUN dotnet build "MngKeeper.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngKeeper.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngKeeper.Api.dll"]
