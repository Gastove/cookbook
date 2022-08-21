FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

WORKDIR /app
COPY . /app

RUN pwd
RUN ls -la

RUN dotnet tool restore
RUN dotnet paket restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:6.0

ENV DOTNET_ENVIRONMENT="Production"

WORKDIR /app

COPY --from=build /app ./

ENTRYPOINT ["dotnet", "Cookbook.dll"]
