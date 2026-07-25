FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json JerseyOs.slnx ./
COPY src/JerseyOs.SharedKernel/JerseyOs.SharedKernel.csproj src/JerseyOs.SharedKernel/
COPY src/JerseyOs.Domain/JerseyOs.Domain.csproj src/JerseyOs.Domain/
COPY src/JerseyOs.Application/JerseyOs.Application.csproj src/JerseyOs.Application/
COPY src/JerseyOs.Infrastructure/JerseyOs.Infrastructure.csproj src/JerseyOs.Infrastructure/
COPY src/JerseyOs.Worker/JerseyOs.Worker.csproj src/JerseyOs.Worker/
RUN dotnet restore src/JerseyOs.Worker/JerseyOs.Worker.csproj

COPY src/ src/
RUN dotnet publish src/JerseyOs.Worker/JerseyOs.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "JerseyOs.Worker.dll"]
