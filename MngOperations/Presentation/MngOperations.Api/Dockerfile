FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5086

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Presentation/MngOperations.Api/MngOperations.Api.csproj", "Presentation/MngOperations.Api/"]
COPY ["Core/MngOperations.Application/MngOperations.Application.csproj", "Core/MngOperations.Application/"]
COPY ["Core/MngOperations.Domain/MngOperations.Domain.csproj", "Core/MngOperations.Domain/"]
COPY ["Infrastructure/MngOperations.Infrastructure/MngOperations.Infrastructure.csproj", "Infrastructure/MngOperations.Infrastructure/"]

RUN dotnet restore "Presentation/MngOperations.Api/MngOperations.Api.csproj"

COPY . .

WORKDIR "/src/Presentation/MngOperations.Api"
RUN dotnet build "MngOperations.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngOperations.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngOperations.Api.dll"]
