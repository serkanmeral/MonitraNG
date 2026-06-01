FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5095

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Presentation/MngDocument.Api/MngDocument.Api.csproj", "Presentation/MngDocument.Api/"]
COPY ["Core/MngDocument.Application/MngDocument.Application.csproj", "Core/MngDocument.Application/"]
COPY ["Core/MngDocument.Domain/MngDocument.Domain.csproj", "Core/MngDocument.Domain/"]
COPY ["Infrastructure/MngDocument.Infrastructure/MngDocument.Infrastructure.csproj", "Infrastructure/MngDocument.Infrastructure/"]

RUN dotnet restore "Presentation/MngDocument.Api/MngDocument.Api.csproj"

COPY . .

WORKDIR "/src/Presentation/MngDocument.Api"
RUN dotnet build "MngDocument.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngDocument.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngDocument.Api.dll"]
