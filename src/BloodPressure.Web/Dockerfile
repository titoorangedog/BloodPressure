FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/BloodPressure.Web/BloodPressure.Web.csproj
RUN dotnet publish src/BloodPressure.Web/BloodPressure.Web.csproj -c Release -o /app/publish

FROM nginx:alpine
WORKDIR /usr/share/nginx/html
COPY --from=build /app/publish/wwwroot .
COPY src/BloodPressure.Web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
