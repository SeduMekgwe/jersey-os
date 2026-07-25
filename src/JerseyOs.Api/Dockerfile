FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json JerseyOs.slnx ./
COPY src/JerseyOs.SharedKernel/JerseyOs.SharedKernel.csproj src/JerseyOs.SharedKernel/
COPY src/JerseyOs.Domain/JerseyOs.Domain.csproj src/JerseyOs.Domain/
COPY src/JerseyOs.Application/JerseyOs.Application.csproj src/JerseyOs.Application/
COPY src/JerseyOs.Infrastructure/JerseyOs.Infrastructure.csproj src/JerseyOs.Infrastructure/
COPY src/JerseyOs.Contracts/JerseyOs.Contracts.csproj src/JerseyOs.Contracts/
COPY src/JerseyOs.Api/JerseyOs.Api.csproj src/JerseyOs.Api/
RUN dotnet restore src/JerseyOs.Api/JerseyOs.Api.csproj

COPY src/ src/
RUN dotnet publish src/JerseyOs.Api/JerseyOs.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "JerseyOs.Api.dll"]
