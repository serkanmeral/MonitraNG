FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5080

# Install system dependencies for backup tools
RUN apt-get update && \
    apt-get install -y \
    curl \
    wget \
    gnupg \
    && rm -rf /var/lib/apt/lists/*

# Install MongoDB Database Tools (mongodump, mongorestore, etc.)
# Note: Using Debian bookworm (not Ubuntu) as .NET 9.0 image is Debian-based
RUN wget -qO - https://www.mongodb.org/static/pgp/server-7.0.asc | gpg --dearmor -o /usr/share/keyrings/mongodb-server-7.0.gpg && \
    echo "deb [ arch=amd64,arm64 signed-by=/usr/share/keyrings/mongodb-server-7.0.gpg ] https://repo.mongodb.org/apt/debian bookworm/mongodb-org/7.0 main" | tee /etc/apt/sources.list.d/mongodb-org-7.0.list && \
    apt-get update && \
    apt-get install -y mongodb-database-tools && \
    rm -rf /var/lib/apt/lists/*

# Install PostgreSQL client tools (pg_dump, pg_restore, etc.)
RUN apt-get update && \
    apt-get install -y postgresql-client && \
    rm -rf /var/lib/apt/lists/*

# Verify installations
RUN mongodump --version && \
    pg_dump --version

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for dependency restoration
COPY ["Presentation/MngAdmin.Api/MngAdmin.Api.csproj", "Presentation/MngAdmin.Api/"]
COPY ["Core/MngAdmin.Application/MngAdmin.Application.csproj", "Core/MngAdmin.Application/"]
COPY ["Core/MngAdmin.Domain/MngAdmin.Domain.csproj", "Core/MngAdmin.Domain/"]
COPY ["Infrastructure/MngAdmin.Infrastructure/MngAdmin.Infrastructure.csproj", "Infrastructure/MngAdmin.Infrastructure/"]
COPY ["Infrastructure/MngAdmin.Persistence/MngAdmin.Persistence.csproj", "Infrastructure/MngAdmin.Persistence/"]

# Restore dependencies
RUN dotnet restore "Presentation/MngAdmin.Api/MngAdmin.Api.csproj"

# Copy all source files
COPY . .

# Build the project
WORKDIR "/src/Presentation/MngAdmin.Api"
RUN dotnet build "MngAdmin.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MngAdmin.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MngAdmin.Api.dll"]
