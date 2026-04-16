# שלב 1: Runtime - משתמשים בתמונה קלה להרצה
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
# Render מקשיב לרוב לפורט 8080 או 10000, נגדיר את ASP.NET להקשיב ל-8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# שלב 2: Build - משתמשים ב-SDK המלא כדי לקמפל
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# העתקת קובץ הפרויקט ושחזור חבילות (Restore)
# שים לב לנתיבים: אנחנו מניחים שה-Root Directory ב-Render הוא 'backend'
COPY ["SmartSupport.Api/SmartSupport.Api.csproj", "SmartSupport.Api/"]
RUN dotnet restore "SmartSupport.Api/SmartSupport.Api.csproj"

# העתקת שאר הקוד וקימפול
COPY . .
WORKDIR "/src/SmartSupport.Api"
RUN dotnet build "SmartSupport.Api.csproj" -c Release -o /app/build

# שלב 3: Publish - יצירת ה-Artifact הסופי
FROM build AS publish
RUN dotnet publish "SmartSupport.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# שלב 4: Final - העתקת התוצאה בלבד והרצה
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartSupport.Api.dll"]