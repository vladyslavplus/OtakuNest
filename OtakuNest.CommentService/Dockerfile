FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OtakuNest.Contracts/OtakuNest.Contracts.csproj OtakuNest.Contracts/
COPY OtakuNest.Common/OtakuNest.Common.csproj OtakuNest.Common/
COPY OtakuNest.CommentService/OtakuNest.CommentService.csproj OtakuNest.CommentService/
RUN dotnet restore OtakuNest.CommentService/OtakuNest.CommentService.csproj

COPY . .
RUN dotnet publish OtakuNest.CommentService/OtakuNest.CommentService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OtakuNest.CommentService.dll"]
