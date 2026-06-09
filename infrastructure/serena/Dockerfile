# Extend the locally-built Serena image with .NET 11 preview SDK + fsautocomplete.
# Requires: docker build -t serena:latest . (run from the Serena repo root first).
#
# Uses a multi-stage copy so the SDK version stays in lock-step with the rest
# of the stack (same tag already used by vault-init in docker-compose.yml).

FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.4 AS dotnet-sdk

FROM serena:latest

COPY --from=dotnet-sdk /usr/share/dotnet /usr/share/dotnet

ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="${PATH}:/usr/share/dotnet:/root/.dotnet/tools"

#
# https://oraios.github.io/serena/01-about/020_programming-languages.html#language-servers
#

# Node.js runtime (for TypeScript, JavaScript, HTML, JSON, SCSS/CSS, YAML language servers)
# and PowerShell runtime (for the PowerShell language server).
ENV POWERSHELL_VERSION=7.4.6
RUN apt-get update && apt-get install -y --no-install-recommends \
        nodejs \
        npm \
        wget \
        ca-certificates \
    && wget -qO /tmp/pwsh.tar.gz \
        "https://github.com/PowerShell/PowerShell/releases/download/v${POWERSHELL_VERSION}/powershell-${POWERSHELL_VERSION}-linux-x64.tar.gz" \
    && mkdir -p /usr/local/share/powershell \
    && tar -xzf /tmp/pwsh.tar.gz -C /usr/local/share/powershell \
    && chmod +x /usr/local/share/powershell/pwsh \
    && rm /tmp/pwsh.tar.gz \
    && ln -s /usr/local/share/powershell/pwsh /usr/local/bin/pwsh \
    && rm -rf /var/lib/apt/lists/*

# serena:latest is Alpine-based and does not ship libicu. Setting invariant mode
# avoids the ICU dependency; fsautocomplete (an LSP server) has no need for
# culture-sensitive string handling so there is no functional loss.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
RUN dotnet tool install --global fsautocomplete

# The serena:latest base image may be built on macOS, which leaves AppleDouble
# metadata files (._*) alongside real files. Serena's YAML scanner picks them up
# and crashes on their binary content. Strip them out at image build time.
RUN find /workspaces/serena -name '._*' -delete

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
