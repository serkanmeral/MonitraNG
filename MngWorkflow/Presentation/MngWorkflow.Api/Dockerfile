FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5085

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Presentation/MngWorkflow.Api/MngWorkflow.Api.csproj", "Presentation/MngWorkflow.Api/"]
COPY ["Core/MngWorkflow.Application/MngWorkflow.Application.csproj", "Core/MngWorkflow.Application/"]
COPY ["Core/MngWorkflow.Domain/MngWorkflow.Domain.csproj", "Core/MngWorkflow.Domain/"]
COPY ["Infrastructure/MngWorkflow.Infrastructure/MngWorkflow.Infrastructure.csproj", "Infrastructure/MngWorkflow.Infrastructure/"]

RUN dotnet restore "Presentation/MngWorkflow.Api/MngWorkflow.Api.csproj"

COPY . .

WORKDIR "/src/Presentation/MngWorkflow.Api"
RUN dotnet build "MngWorkflow.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngWorkflow.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:5085
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngWorkflow.Api.dll"]
