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
