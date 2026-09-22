FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SelfRestaurant.Microservices.sln ./
COPY src/Services/SelfRestaurant.Catalog.Api/SelfRestaurant.Catalog.Api.csproj src/Services/SelfRestaurant.Catalog.Api/

RUN dotnet restore src/Services/SelfRestaurant.Catalog.Api/SelfRestaurant.Catalog.Api.csproj

COPY src/ ./src/

RUN dotnet publish src/Services/SelfRestaurant.Catalog.Api/SelfRestaurant.Catalog.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish/ ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SelfRestaurant.Catalog.Api.dll"]
