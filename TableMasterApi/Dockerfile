# Build image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /App

# Restore the API independently to keep the image cache deterministic.
COPY TableMasterApi/TableMasterApi.csproj TableMasterApi/
RUN dotnet restore TableMasterApi/TableMasterApi.csproj

# Copy only the API sources. Secrets are excluded by .dockerignore.
COPY TableMasterApi/ TableMasterApi/
RUN dotnet publish TableMasterApi/TableMasterApi.csproj \
    --no-restore \
    --configuration Release \
    --output /out

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /App

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copier les fichiers publiés
COPY --from=build /out ./

# Définir l'URL sur laquelle l'application écoute
ENV ASPNETCORE_URLS=http://+:8080

# Exposer le port
EXPOSE 8080

# Point d'entrée
ENTRYPOINT ["dotnet", "TableMasterApi.dll"]
