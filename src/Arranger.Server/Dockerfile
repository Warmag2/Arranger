FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
RUN apk add --no-cache tzdata
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app

FROM localhost/faemiyahsdk:latest AS build
WORKDIR /build
COPY ["src/", "src/"]
WORKDIR "/build/src/Arranger.Server"
RUN dotnet publish "Arranger.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --chown=app:app --from=build /app/publish .
RUN mkdir -p /app/dpkeys && chown app:app /app/dpkeys
USER app
ENTRYPOINT ["dotnet", "Faemiyah.Arranger.Server.dll"]
