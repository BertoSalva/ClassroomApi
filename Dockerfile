# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore ./src/Classroom.Api/Classroom.Api.csproj
RUN dotnet publish ./src/Classroom.Api/Classroom.Api.csproj -c Release -o /app/publish

# ---- run ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./
EXPOSE 8080

ENTRYPOINT ["dotnet", "Classroom.Api.dll"]
