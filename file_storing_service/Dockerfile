FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app

# Копируем файлы проекта
COPY . .
RUN mkdir -p /app/Properties
RUN echo '<Project Sdk="Microsoft.NET.Sdk.Web">\n\
  <PropertyGroup>\n\
    <TargetFramework>net7.0</TargetFramework>\n\
    <Nullable>enable</Nullable>\n\
    <ImplicitUsings>enable</ImplicitUsings>\n\
  </PropertyGroup>\n\
  <ItemGroup>\n\
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.2.3" />\n\
  </ItemGroup>\n\
</Project>' > FileStoringService.csproj

# Восстанавливаем зависимости и собираем проект
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Создаем образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "FileStoringService.dll"]
