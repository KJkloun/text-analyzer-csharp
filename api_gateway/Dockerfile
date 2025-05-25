FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app

# Копируем файлы проекта
COPY . .
RUN mkdir -p /app/Properties
RUN echo '<Project Sdk="Microsoft.NET.Sdk.Web">\n  <PropertyGroup>\n    <GenerateDocumentationFile>true</GenerateDocumentationFile>\n    <NoWarn>$(NoWarn);1591</NoWarn>\n  </PropertyGroup>\n</Project>' > /app/Properties/Documentation.props

# Восстанавливаем зависимости и собираем проект
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Создаем образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 8080
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
