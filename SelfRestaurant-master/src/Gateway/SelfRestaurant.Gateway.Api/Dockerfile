FROM node:22-alpine AS customer_frontend
WORKDIR /frontend
COPY src/Frontend/selfrestaurant-customer-web/package.json ./
RUN npm install
COPY src/Frontend/selfrestaurant-customer-web/ ./
RUN npm run build

FROM node:22-alpine AS chef_frontend
WORKDIR /frontend
COPY src/Frontend/selfrestaurant-chef-web/package.json ./
RUN npm install
COPY src/Frontend/selfrestaurant-chef-web/ ./
RUN npm run build

FROM node:22-alpine AS cashier_frontend
WORKDIR /frontend
COPY src/Frontend/selfrestaurant-cashier-web/package.json ./
RUN npm install
COPY src/Frontend/selfrestaurant-cashier-web/ ./
RUN npm run build

FROM node:22-alpine AS admin_frontend
WORKDIR /frontend
COPY src/Frontend/selfrestaurant-admin-web/package.json ./
RUN npm install
COPY src/Frontend/selfrestaurant-admin-web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SelfRestaurant.Microservices.sln ./
COPY src/Gateway/SelfRestaurant.Gateway.Api/SelfRestaurant.Gateway.Api.csproj src/Gateway/SelfRestaurant.Gateway.Api/

RUN dotnet restore src/Gateway/SelfRestaurant.Gateway.Api/SelfRestaurant.Gateway.Api.csproj

COPY src/ ./src/

RUN dotnet publish src/Gateway/SelfRestaurant.Gateway.Api/SelfRestaurant.Gateway.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false
COPY --from=customer_frontend /frontend/dist/ /app/publish/wwwroot/customer-app/
COPY --from=chef_frontend /frontend/dist/ /app/publish/wwwroot/chef-app/
COPY --from=cashier_frontend /frontend/dist/ /app/publish/wwwroot/cashier-app/
COPY --from=admin_frontend /frontend/dist/ /app/publish/wwwroot/admin-app/

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish/ ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SelfRestaurant.Gateway.Api.dll"]
