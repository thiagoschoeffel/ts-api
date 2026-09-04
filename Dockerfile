FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Ts.Api.sln Directory.Build.props global.json ./
COPY src/Ts.Api.Api/Ts.Api.Api.csproj src/Ts.Api.Api/
COPY src/Ts.Api.Application/Ts.Api.Application.csproj src/Ts.Api.Application/
COPY src/Ts.Api.Domain/Ts.Api.Domain.csproj src/Ts.Api.Domain/
COPY src/Ts.Api.Infrastructure/Ts.Api.Infrastructure.csproj src/Ts.Api.Infrastructure/
RUN dotnet restore src/Ts.Api.Api/Ts.Api.Api.csproj

COPY src/ src/
RUN dotnet publish src/Ts.Api.Api/Ts.Api.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ts.Api.Api.dll"]
