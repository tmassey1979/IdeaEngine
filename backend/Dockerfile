FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish backend/src/Dragon.Backend.Cli/Dragon.Backend.Cli.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl gnupg bash \
    && mkdir -p /etc/apt/keyrings \
    && curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg | dd of=/etc/apt/keyrings/githubcli-archive-keyring.gpg \
    && chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" > /etc/apt/sources.list.d/github-cli.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends gh \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish/ ./
COPY docker/backend-entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENV DRAGON_ROOT=/workspace \
    DRAGON_STATUS_PREFIX=http://+:5078/ \
    DRAGON_RUN_MODE=watch \
    DRAGON_POLL_SECONDS=30 \
    DRAGON_MAX_PASSES=10 \
    DRAGON_IDLE_PASSES=2 \
    DRAGON_MAX_CYCLES=100 \
    GH_BIN=/usr/bin/gh

EXPOSE 5078

ENTRYPOINT ["/entrypoint.sh"]
