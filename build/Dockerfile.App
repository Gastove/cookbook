FROM registry.gitlab.com/gastove/dotnet-builder:1.0 AS build

WORKDIR /build
COPY . /build
RUN dotnet publish -c Release -o /app

FROM build

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

WORKDIR /app

ENTRYPOINT ["dotnet", "Cookbook.dll"]
