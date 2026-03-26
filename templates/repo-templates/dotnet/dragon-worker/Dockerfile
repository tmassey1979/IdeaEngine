ARG CODEX_CLI_VERSION=0.116.0

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Dragon.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
ARG CODEX_CLI_VERSION
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*
RUN curl -fsSL "https://registry.npmjs.org/%40openai%2fcodex/-/codex-${CODEX_CLI_VERSION}-linux-x64.tgz" -o /tmp/codex.tgz \
    && mkdir -p /opt/codex \
    && tar -xzf /tmp/codex.tgz -C /opt/codex \
    && ln -sf /opt/codex/package/vendor/x86_64-unknown-linux-musl/codex/codex /usr/local/bin/codex \
    && ln -sf /opt/codex/package/vendor/x86_64-unknown-linux-musl/path/rg /usr/local/bin/rg \
    && rm -f /tmp/codex.tgz
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Dragon.Worker.dll"]
