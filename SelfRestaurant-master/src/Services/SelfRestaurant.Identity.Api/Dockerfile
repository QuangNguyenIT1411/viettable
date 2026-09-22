FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SelfRestaurant.Microservices.sln ./
COPY src/Services/SelfRestaurant.Identity.Api/SelfRestaurant.Identity.Api.csproj src/Services/SelfRestaurant.Identity.Api/

RUN dotnet restore src/Services/SelfRestaurant.Identity.Api/SelfRestaurant.Identity.Api.csproj

COPY src/ ./src/

RUN dotnet publish src/Services/SelfRestaurant.Identity.Api/SelfRestaurant.Identity.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish/ ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SelfRestaurant.Identity.Api.dll"]
