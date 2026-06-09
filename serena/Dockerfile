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

RUN dotnet tool install --global fsautocomplete
