FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

WORKDIR /build
COPY . /build

RUN dotnet tool restore
RUN dotnet paket restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:6.0

ENV DOTNET_ENVIRONMENT="Production"
ENV PORT="8080"
# This is completely stupid, and: on startup, Kestrel _really_ wants to do
# HTTPS. I don't want it to -- I terminate HTTPS elsewhere. The only way to
# force it to stop appears to be setting this var, which then errors with a cute
# message saying we're gonna override from the appsettings.json instead. So then
# I _also_ have to set the Kestrel Endpoints var, to specify the binding to use
# in Cloud Run.
ENV ASPNETCORE_URLS="http://0.0.0.0:${PORT}"
ENV KESTREL__ENDPOINTS__HTTP__URL="http://0.0.0.0:${PORT}"

WORKDIR /app

COPY --from=build /app ./

ENTRYPOINT ["dotnet", "Cookbook.dll"]
