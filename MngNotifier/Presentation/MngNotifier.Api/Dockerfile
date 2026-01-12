FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5070

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for dependency restoration
COPY ["Presentation/MngNotifier.Api/MngNotifier.Api.csproj", "Presentation/MngNotifier.Api/"]
COPY ["Core/MngNotifier.Application/MngNotifier.Application.csproj", "Core/MngNotifier.Application/"]
COPY ["Core/MngNotifier.Domain/MngNotifier.Domain.csproj", "Core/MngNotifier.Domain/"]
COPY ["Infrastructure/MngNotifier.Infrastructure/MngNotifier.Infrastructure.csproj", "Infrastructure/MngNotifier.Infrastructure/"]
COPY ["Infrastructure/MngNotifier.Persistence/MngNotifier.Persistence.csproj", "Infrastructure/MngNotifier.Persistence/"]

# Restore dependencies
RUN dotnet restore "Presentation/MngNotifier.Api/MngNotifier.Api.csproj"

# Copy all source files
COPY . .

# Build the project
WORKDIR "/src/Presentation/MngNotifier.Api"
RUN dotnet build "MngNotifier.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngNotifier.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngNotifier.Api.dll"]
