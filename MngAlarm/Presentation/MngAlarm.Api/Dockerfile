FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5087

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Presentation/MngAlarm.Api/MngAlarm.Api.csproj", "Presentation/MngAlarm.Api/"]
COPY ["Core/MngAlarm.Application/MngAlarm.Application.csproj", "Core/MngAlarm.Application/"]
COPY ["Core/MngAlarm.Domain/MngAlarm.Domain.csproj", "Core/MngAlarm.Domain/"]
COPY ["Infrastructure/MngAlarm.Infrastructure/MngAlarm.Infrastructure.csproj", "Infrastructure/MngAlarm.Infrastructure/"]

RUN dotnet restore "Presentation/MngAlarm.Api/MngAlarm.Api.csproj"

COPY . .

WORKDIR "/src/Presentation/MngAlarm.Api"
RUN dotnet build "MngAlarm.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngAlarm.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:5087
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngAlarm.Api.dll"]
