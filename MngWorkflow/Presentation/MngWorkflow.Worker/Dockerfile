FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Presentation/MngWorkflow.Worker/MngWorkflow.Worker.csproj", "Presentation/MngWorkflow.Worker/"]
COPY ["Core/MngWorkflow.Application/MngWorkflow.Application.csproj", "Core/MngWorkflow.Application/"]
COPY ["Core/MngWorkflow.Domain/MngWorkflow.Domain.csproj", "Core/MngWorkflow.Domain/"]
COPY ["Infrastructure/MngWorkflow.Infrastructure/MngWorkflow.Infrastructure.csproj", "Infrastructure/MngWorkflow.Infrastructure/"]

RUN dotnet restore "Presentation/MngWorkflow.Worker/MngWorkflow.Worker.csproj"
COPY . .
WORKDIR "/src/Presentation/MngWorkflow.Worker"
RUN dotnet build "MngWorkflow.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngWorkflow.Worker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngWorkflow.Worker.dll"]
