# Profile service (:5066). Avatar bytes live in Postgres, so this container stays stateless
# and needs no volume.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# csproj first so a source-only change reuses the restore layer.
COPY explAInedProfileService.csproj ./
RUN dotnet restore explAInedProfileService.csproj

COPY . .
RUN dotnet publish explAInedProfileService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish ./

# Same port inside the container as on the host, so the upstream URLs in appsettings
# (Upstreams:ArticlesBaseUrl, Upstreams:CommentsBaseUrl) keep meaning the same thing under
# a 1:1 compose mapping.
ENV ASPNETCORE_HTTP_PORTS=5066
EXPOSE 5066

# Migrations auto-apply at startup (db.Database.Migrate()), so Postgres must be reachable
# before this container starts — no separate migration step.
USER $APP_UID
ENTRYPOINT ["dotnet", "explAInedProfileService.dll"]
