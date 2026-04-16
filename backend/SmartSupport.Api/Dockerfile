# שלב 1: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# שלב 2: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# העתקת קובץ הפרויקט (מתוך הנחה שה-Root Directory הוא 'backend')
COPY ["SmartSupport.Api/SmartSupport.Api.csproj", "SmartSupport.Api/"]
RUN dotnet restore "SmartSupport.Api/SmartSupport.Api.csproj"

# העתקת שאר הקוד
COPY . .
WORKDIR "/src/SmartSupport.Api"
RUN dotnet build "SmartSupport.Api.csproj" -c Release -o /app/build

# שלב 3: Publish
FROM build AS publish
RUN dotnet publish "SmartSupport.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# שלב 4: Final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartSupport.Api.dll"]