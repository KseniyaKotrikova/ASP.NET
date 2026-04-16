# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Копируем файлы проектов и выполняем restore
COPY src/ .

COPY src/PromoCodeFactory.Core/*.csproj ./src/PromoCodeFactory.Core/
COPY src/PromoCodeFactory.DataAccess/*.csproj ./src/PromoCodeFactory.DataAccess/
COPY src/PromoCodeFactory.WebHost/*.csproj ./src/PromoCodeFactory.WebHost/

RUN dotnet restore

# Копируем остальные файлы и собираем проект
COPY src/ .
WORKDIR "/app/PromoCodeFactory.WebHost"
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Создаем папку для БД, чтобы права доступа были верными
RUN mkdir db

ENTRYPOINT ["dotnet", "PromoCodeFactory.WebHost.dll"]
