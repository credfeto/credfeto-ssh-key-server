FROM mcr.microsoft.com/dotnet/runtime-deps:11.0-azurelinux3.0-distroless@sha256:44897627d08bbea1df6799b0fb7139908e3808b9e719eaa4b6056125a1ea7086

WORKDIR /usr/src/app

# Bundle App and basic config
COPY Credfeto.Keys.Server .
COPY appsettings.json .

EXPOSE 8080

ENTRYPOINT [ "/usr/src/app/Credfeto.Keys.Server" ]

# Perform a healthcheck. Note that ECS ignores this, so this is for local development
HEALTHCHECK --interval=5s --timeout=2s --retries=3 --start-period=5s CMD [ "/usr/src/app/Credfeto.Keys.Server", "--health-check", "http://127.0.0.1:8080/ping?source=docker" ]
