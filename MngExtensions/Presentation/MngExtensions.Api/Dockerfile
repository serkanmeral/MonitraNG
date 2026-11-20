FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5092

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Presentation/MngExtensions.Api/MngExtensions.Api.csproj", "Presentation/MngExtensions.Api/"]
COPY ["Core/MngExtensions.Application/MngExtensions.Application.csproj", "Core/MngExtensions.Application/"]
COPY ["Core/MngExtensions.Domain/MngExtensions.Domain.csproj", "Core/MngExtensions.Domain/"]
COPY ["Infrastructure/MngExtensions.Infrastructure/MngExtensions.Infrastructure.csproj", "Infrastructure/MngExtensions.Infrastructure/"]
COPY ["Infrastructure/MngExtensions.Persistence/MngExtensions.Persistence.csproj", "Infrastructure/MngExtensions.Persistence/"]

RUN dotnet restore "Presentation/MngExtensions.Api/MngExtensions.Api.csproj"

COPY . .

WORKDIR "/src/Presentation/MngExtensions.Api"
RUN dotnet build "MngExtensions.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngExtensions.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngExtensions.Api.dll"]
