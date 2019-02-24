FROM microsoft/dotnet:2.2-sdk AS build

ENV LANG=C.UTF-8

WORKDIR /build
COPY . /build

RUN dotnet tool install -g paket
ENV PATH="$PATH:/root/.dotnet/tools"

RUN dotnet publish -c Release -o /app

FROM microsoft/dotnet:2.2-runtime

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

WORKDIR /app

COPY --from=build /app ./

ENTRYPOINT ["dotnet", "Cookbook.dll"]
