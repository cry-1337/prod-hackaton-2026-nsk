ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

FROM ${SDK_IMAGE} AS build
WORKDIR /src
COPY Backend.slnx global.json Directory.Build.props Directory.Packages.props ./
COPY src/Backend.Api/Backend.Api.csproj src/Backend.Api/
COPY src/Backend.Application/Backend.Application.csproj src/Backend.Application/
COPY src/Backend.Domain/Backend.Domain.csproj src/Backend.Domain/
COPY src/Backend.Infrastructure/Backend.Infrastructure.csproj src/Backend.Infrastructure/
RUN dotnet restore src/Backend.Api/Backend.Api.csproj
COPY src/ src/
RUN dotnet publish src/Backend.Api -c Release --no-restore -o /publish -p:UseAppHost=false

FROM ${ASPNET_IMAGE}
WORKDIR /app
COPY --from=build /publish/ ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backend.Api.dll"]
