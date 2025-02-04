# Build image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /App

# Copy everything and restore dependencies
COPY . ./
RUN dotnet restore

# Build and publish a release
RUN dotnet publish -c Release -o /out

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App

# Copier les fichiers publiés
COPY --from=build /out ./

# Définir l'URL sur laquelle l'application écoute
ENV ASPNETCORE_URLS=http://+:8080

# Exposer le port
EXPOSE 8080

# Point d'entrée
ENTRYPOINT ["dotnet", "TableMasterApi.dll"]
