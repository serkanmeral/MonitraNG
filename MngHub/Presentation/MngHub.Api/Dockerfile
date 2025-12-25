FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5020

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Presentation/MngHub.Api/MngHub.Api.csproj", "Presentation/MngHub.Api/"]
COPY ["Core/MngHub.Application/MngHub.Application.csproj", "Core/MngHub.Application/"]
COPY ["Core/MngHub.Domain/MngHub.Domain.csproj", "Core/MngHub.Domain/"]
COPY ["Infrastructure/MngHub.Infrastructure/MngHub.Infrastructure.csproj", "Infrastructure/MngHub.Infrastructure/"]
COPY ["Infrastructure/MngHub.Persistence/MngHub.Persistence.csproj", "Infrastructure/MngHub.Persistence/"]
RUN dotnet restore "Presentation/MngHub.Api/MngHub.Api.csproj"
COPY . .
WORKDIR "/src/Presentation/MngHub.Api"
RUN dotnet build "MngHub.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngHub.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngHub.Api.dll"]

