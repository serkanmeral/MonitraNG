FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5010

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for dependency restoration
COPY ["Presentation/MngDataGateway.Api/MngDataGateway.Api.csproj", "Presentation/MngDataGateway.Api/"]
COPY ["Core/MngDataGateway.Application/MngDataGateway.Application.csproj", "Core/MngDataGateway.Application/"]
COPY ["Core/MngDataGateway.Domain/MngDataGateway.Domain.csproj", "Core/MngDataGateway.Domain/"]
COPY ["Infrastructure/MngDataGateway.Infrastructure/MngDataGateway.Infrastructure.csproj", "Infrastructure/MngDataGateway.Infrastructure/"]
COPY ["Infrastructure/MngDataGateway.Persistence/MngDataGateway.Persistence.csproj", "Infrastructure/MngDataGateway.Persistence/"]

# Restore dependencies
RUN dotnet restore "Presentation/MngDataGateway.Api/MngDataGateway.Api.csproj"

# Copy all source files
COPY . .

# Build the project
WORKDIR "/src/Presentation/MngDataGateway.Api"
RUN dotnet build "MngDataGateway.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngDataGateway.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngDataGateway.Api.dll"]

