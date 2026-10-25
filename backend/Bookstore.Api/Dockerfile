FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/Bookstore.Api/Bookstore.Api.csproj backend/Bookstore.Api/
RUN dotnet restore backend/Bookstore.Api/Bookstore.Api.csproj
COPY backend/Bookstore.Api/ backend/Bookstore.Api/
RUN dotnet publish backend/Bookstore.Api/Bookstore.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Bookstore.Api.dll"]
