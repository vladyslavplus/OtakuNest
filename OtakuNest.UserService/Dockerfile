FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OtakuNest.Contracts/OtakuNest.Contracts.csproj OtakuNest.Contracts/
COPY OtakuNest.UserService/OtakuNest.UserService.csproj OtakuNest.UserService/
RUN dotnet restore OtakuNest.UserService/OtakuNest.UserService.csproj

COPY . .
RUN dotnet publish OtakuNest.UserService/OtakuNest.UserService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OtakuNest.UserService.dll"]
