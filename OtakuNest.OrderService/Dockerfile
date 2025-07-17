FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OtakuNest.Contracts/OtakuNest.Contracts.csproj OtakuNest.Contracts/
COPY OtakuNest.OrderService/OtakuNest.OrderService.csproj OtakuNest.OrderService/
RUN dotnet restore OtakuNest.OrderService/OtakuNest.OrderService.csproj

COPY . .
RUN dotnet publish OtakuNest.OrderService/OtakuNest.OrderService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OtakuNest.OrderService.dll"]
