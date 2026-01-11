FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5030

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for dependency restoration
COPY ["Presentation/MngLLM.Api/MngLLM.Api.csproj", "Presentation/MngLLM.Api/"]
COPY ["Core/MngLLM.Application/MngLLM.Application.csproj", "Core/MngLLM.Application/"]
COPY ["Core/MngLLM.Domain/MngLLM.Domain.csproj", "Core/MngLLM.Domain/"]
COPY ["Infrastructure/MngLLM.Infrastructure/MngLLM.Infrastructure.csproj", "Infrastructure/MngLLM.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "Presentation/MngLLM.Api/MngLLM.Api.csproj"

# Copy all source files
COPY . .

# Build the project
WORKDIR "/src/Presentation/MngLLM.Api"
RUN dotnet build "MngLLM.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngLLM.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngLLM.Api.dll"]
