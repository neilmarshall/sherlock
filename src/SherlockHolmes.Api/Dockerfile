# syntax=docker/dockerfile:1.7

# --- frontend build ---
FROM node:22-alpine AS frontend
WORKDIR /frontend
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci
COPY src/frontend/ ./
RUN npm run build

# --- api build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /api
COPY src/SherlockHolmes.Api/SherlockHolmes.Api.csproj ./
RUN dotnet restore SherlockHolmes.Api.csproj
COPY src/SherlockHolmes.Api/ ./
RUN dotnet publish SherlockHolmes.Api.csproj \
    -c Release \
    -o /publish \
    --no-restore

# --- runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api-build /publish ./
COPY --from=frontend /frontend/dist ./wwwroot
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SherlockHolmes.Api.dll"]
