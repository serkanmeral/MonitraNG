FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5091

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Presentation/MngLogCollector.Api/MngLogCollector.Api.csproj", "Presentation/MngLogCollector.Api/"]
COPY ["Core/MngLogCollector.Application/MngLogCollector.Application.csproj", "Core/MngLogCollector.Application/"]
COPY ["Core/MngLogCollector.Domain/MngLogCollector.Domain.csproj", "Core/MngLogCollector.Domain/"]
COPY ["Infrastructure/MngLogCollector.Persistence/MngLogCollector.Persistence.csproj", "Infrastructure/MngLogCollector.Persistence/"]
COPY ["Infrastructure/MngLogCollector.Infrastructure/MngLogCollector.Infrastructure.csproj", "Infrastructure/MngLogCollector.Infrastructure/"]

RUN dotnet restore "Presentation/MngLogCollector.Api/MngLogCollector.Api.csproj"

COPY . .

WORKDIR "/src/Presentation/MngLogCollector.Api"
RUN dotnet build "MngLogCollector.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngLogCollector.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:5091
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngLogCollector.Api.dll"]
