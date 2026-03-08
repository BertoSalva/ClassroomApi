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
RUN mkdir -p /secrets

EXPOSE 8080

CMD /bin/sh -c "test -n \"$GCP_SA_JSON_B64\" || (echo 'Missing GCP_SA_JSON_B64' >&2; exit 1); printf '%s' \"$GCP_SA_JSON_B64\" | base64 -d > /secrets/gcp.json; export GOOGLE_APPLICATION_CREDENTIALS=/secrets/gcp.json; dotnet Classroom.Api.dll"