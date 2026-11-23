# SmartSupport API. Build context: the backend/ folder (Railway: Root Directory = /backend).

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first, using only the project files, so package downloads are cached between builds.
COPY SmartSupport.sln ./
COPY SmartSupport.Domain/SmartSupport.Domain.csproj SmartSupport.Domain/
COPY SmartSupport.Application/SmartSupport.Application.csproj SmartSupport.Application/
COPY SmartSupport.Infrastructure/SmartSupport.Infrastructure.csproj SmartSupport.Infrastructure/
COPY SmartSupport.Api/SmartSupport.Api.csproj SmartSupport.Api/
RUN dotnet restore SmartSupport.Api/SmartSupport.Api.csproj

COPY . .
RUN dotnet publish SmartSupport.Api/SmartSupport.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Run ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Secrets come from environment variables (Railway Variables), never from files in the image.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

# Run as the image's built-in non-root user.
USER app

ENTRYPOINT ["dotnet", "SmartSupport.Api.dll"]
