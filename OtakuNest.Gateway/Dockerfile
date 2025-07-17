FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OtakuNest.Common/OtakuNest.Common.csproj OtakuNest.Common/
COPY OtakuNest.Gateway/OtakuNest.Gateway.csproj OtakuNest.Gateway/
RUN dotnet restore OtakuNest.Gateway/OtakuNest.Gateway.csproj

COPY . .
RUN dotnet publish OtakuNest.Gateway/OtakuNest.Gateway.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OtakuNest.Gateway.dll"]
