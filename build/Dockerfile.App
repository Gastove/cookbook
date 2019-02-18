FROM registry.gitlab.com/gastove/cookbook/builder:latest AS build

WORKDIR /build
COPY . /build
RUN dotnet publish -c Release -o /app

FROM build
WORKDIR /app

EXPOSE 5000

ENTRYPOINT ["dotnet", "Cookbook.dll"]
