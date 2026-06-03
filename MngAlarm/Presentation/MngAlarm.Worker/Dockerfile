FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Presentation/MngAlarm.Worker/MngAlarm.Worker.csproj", "Presentation/MngAlarm.Worker/"]
COPY ["Core/MngAlarm.Application/MngAlarm.Application.csproj", "Core/MngAlarm.Application/"]
COPY ["Core/MngAlarm.Domain/MngAlarm.Domain.csproj", "Core/MngAlarm.Domain/"]
COPY ["Infrastructure/MngAlarm.Infrastructure/MngAlarm.Infrastructure.csproj", "Infrastructure/MngAlarm.Infrastructure/"]

RUN dotnet restore "Presentation/MngAlarm.Worker/MngAlarm.Worker.csproj"
COPY . .
WORKDIR "/src/Presentation/MngAlarm.Worker"
RUN dotnet build "MngAlarm.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngAlarm.Worker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngAlarm.Worker.dll"]
