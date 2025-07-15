FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OtakuNest.Contracts/OtakuNest.Contracts.csproj OtakuNest.Contracts/
COPY OtakuNest.CartService/OtakuNest.CartService.csproj OtakuNest.CartService/
RUN dotnet restore OtakuNest.CartService/OtakuNest.CartService.csproj

COPY . .
RUN dotnet publish OtakuNest.CartService/OtakuNest.CartService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OtakuNest.CartService.dll"]
