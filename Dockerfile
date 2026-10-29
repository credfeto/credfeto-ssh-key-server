FROM mcr.microsoft.com/dotnet/runtime-deps:11.0-azurelinux3.0-distroless@sha256:b223c70ffa2a1d97efbb917b690b944292afce07f3158f349491e3465ba3cf36

WORKDIR /usr/src/app

# Bundle App and basic config
COPY Credfeto.Keys.Server .
COPY appsettings.json .

EXPOSE 8080

ENTRYPOINT [ "/usr/src/app/Credfeto.Keys.Server" ]

# Perform a healthcheck. Note that ECS ignores this, so this is for local development
HEALTHCHECK --interval=5s --timeout=2s --retries=3 --start-period=5s CMD [ "/usr/src/app/Credfeto.Keys.Server", "--health-check", "http://127.0.0.1:8080/ping?source=docker" ]
