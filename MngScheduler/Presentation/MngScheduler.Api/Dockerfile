FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5090

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for dependency restoration
COPY ["Presentation/MngScheduler.Api/MngScheduler.Api.csproj", "Presentation/MngScheduler.Api/"]
COPY ["Core/MngScheduler.Application/MngScheduler.Application.csproj", "Core/MngScheduler.Application/"]
COPY ["Core/MngScheduler.Domain/MngScheduler.Domain.csproj", "Core/MngScheduler.Domain/"]
COPY ["Infrastructure/MngScheduler.Infrastructure/MngScheduler.Infrastructure.csproj", "Infrastructure/MngScheduler.Infrastructure/"]
COPY ["Infrastructure/MngScheduler.Persistence/MngScheduler.Persistence.csproj", "Infrastructure/MngScheduler.Persistence/"]

# Restore dependencies
RUN dotnet restore "Presentation/MngScheduler.Api/MngScheduler.Api.csproj"

# Copy all source files
COPY . .

# Build the project
WORKDIR "/src/Presentation/MngScheduler.Api"
RUN dotnet build "MngScheduler.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngScheduler.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngScheduler.Api.dll"]
