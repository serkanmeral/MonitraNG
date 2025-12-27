FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for dependency restoration
COPY ["Presentation/MngGateway.Api/MngGateway.Api.csproj", "Presentation/MngGateway.Api/"]
COPY ["Core/MngGateway.Application/MngGateway.Application.csproj", "Core/MngGateway.Application/"]
COPY ["Core/MngGateway.Domain/MngGateway.Domain.csproj", "Core/MngGateway.Domain/"]
COPY ["Infrastructure/MngGateway.Infrastructure/MngGateway.Infrastructure.csproj", "Infrastructure/MngGateway.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "Presentation/MngGateway.Api/MngGateway.Api.csproj"

# Copy all source files
COPY . .

# Build the project
WORKDIR "/src/Presentation/MngGateway.Api"
RUN dotnet build "MngGateway.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngGateway.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngGateway.Api.dll"]

