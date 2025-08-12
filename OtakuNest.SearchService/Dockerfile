FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OtakuNest.Contracts/OtakuNest.Contracts.csproj OtakuNest.Contracts/
COPY OtakuNest.SearchService/OtakuNest.SearchService.csproj OtakuNest.SearchService/
RUN dotnet restore OtakuNest.SearchService/OtakuNest.SearchService.csproj

COPY . .
RUN dotnet publish OtakuNest.SearchService/OtakuNest.SearchService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OtakuNest.SearchService.dll"]