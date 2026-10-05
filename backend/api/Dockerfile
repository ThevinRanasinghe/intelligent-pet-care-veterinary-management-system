# PetCare API — container build for cloud deployment (Render, Railway, etc.)
# Build context must be the REPOSITORY ROOT:
#   docker build -f backend/api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/api/ ./
RUN dotnet restore PetCare.sln
RUN dotnet publish src/PetCare.Api/PetCare.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render (and most PaaS) inject $PORT; bind the app to it.
CMD ASPNETCORE_URLS="http://+:${PORT:-8080}" dotnet PetCare.Api.dll
