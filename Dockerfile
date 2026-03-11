# syntax=docker/dockerfile:1.7

FROM node:20-bookworm-slim AS frontend-build

WORKDIR /src

COPY package.json yarn.lock .yarnrc tsconfig.json ./
COPY frontend ./frontend

RUN corepack enable && yarn install --frozen-lockfile
RUN yarn build --env production


FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS backend-build

ARG TARGET_RUNTIME=linux-x64

WORKDIR /src

COPY global.json ./
COPY LICENSE.md ./
COPY Logo ./Logo
COPY src ./src

RUN dotnet restore src/NzbDrone.Console/Sonarr.Console.csproj \
    -r ${TARGET_RUNTIME} \
    -p:EnableAnalyzers=false \
    -p:TreatWarningsAsErrors=false
RUN dotnet restore src/NzbDrone.Update/Sonarr.Update.csproj \
    -r ${TARGET_RUNTIME} \
    -p:EnableAnalyzers=false \
    -p:TreatWarningsAsErrors=false
RUN dotnet restore src/NzbDrone.Mono/Sonarr.Mono.csproj \
    -r ${TARGET_RUNTIME} \
    -p:EnableAnalyzers=false \
    -p:TreatWarningsAsErrors=false
RUN dotnet publish src/NzbDrone.Console/Sonarr.Console.csproj \
    -c Release \
    -f net10.0 \
    -r ${TARGET_RUNTIME} \
    --self-contained true \
    --no-restore \
    -p:EnableAnalyzers=false \
    -p:TreatWarningsAsErrors=false \
    -o /app
RUN dotnet publish src/NzbDrone.Update/Sonarr.Update.csproj \
    -c Release \
    -f net10.0 \
    -r ${TARGET_RUNTIME} \
    --self-contained true \
    --no-restore \
    -p:EnableAnalyzers=false \
    -p:TreatWarningsAsErrors=false \
    -o /app/Sonarr.Update
RUN dotnet publish src/NzbDrone.Mono/Sonarr.Mono.csproj \
    -c Release \
    -f net10.0 \
    -r ${TARGET_RUNTIME} \
    --self-contained true \
    --no-restore \
    -p:EnableAnalyzers=false \
    -p:TreatWarningsAsErrors=false \
    -o /tmp/sonarr-mono
RUN cp /tmp/sonarr-mono/Sonarr.Mono.* /app/ && \
    cp /tmp/sonarr-mono/Mono.Posix.NETStandard.* /app/ && \
    cp /tmp/sonarr-mono/libMonoPosixHelper.* /app/ && \
    cp /tmp/sonarr-mono/Sonarr.Mono.* /app/Sonarr.Update/ && \
    cp /tmp/sonarr-mono/Mono.Posix.NETStandard.* /app/Sonarr.Update/ && \
    cp /tmp/sonarr-mono/libMonoPosixHelper.* /app/Sonarr.Update/
RUN cp LICENSE.md /app/


FROM debian:bookworm-slim AS runtime

ENV DEBIAN_FRONTEND=noninteractive

WORKDIR /app

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        libicu72 \
        libsqlite3-0 \
        sqlite3 \
        tini \
        tzdata && \
    rm -rf /var/lib/apt/lists/*

COPY --from=backend-build /app/ /app/
COPY --from=frontend-build /src/_output/UI /app/UI

RUN chmod 755 /app/Sonarr /app/Sonarr.Update && \
    find /app -type f -name ffprobe -exec chmod 755 {} \;

EXPOSE 8989

VOLUME ["/config", "/tv", "/downloads"]

ENTRYPOINT ["/usr/bin/tini", "--"]
CMD ["/app/Sonarr", "-nobrowser", "-data=/config"]
