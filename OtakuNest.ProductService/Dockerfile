FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OtakuNest.Contracts/OtakuNest.Contracts.csproj OtakuNest.Contracts/
COPY OtakuNest.ProductService/OtakuNest.ProductService.csproj OtakuNest.ProductService/
RUN dotnet restore OtakuNest.ProductService/OtakuNest.ProductService.csproj

COPY . .
RUN dotnet publish OtakuNest.ProductService/OtakuNest.ProductService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OtakuNest.ProductService.dll"]
