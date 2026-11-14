FROM mcr.microsoft.com/dotnet/runtime-deps:11.0-resolute-chiseled-extra@sha256:40550ff1ce9a725731cfb8340b3dc179170a55591de535d4fe39ffed7bfcc347

WORKDIR /usr/src/app

# Bundle App and basic config
COPY Credfeto.Keys.Server .
COPY appsettings.json .

# Fail the build if the binary cannot load its native dependencies on this base image
RUN [ "/usr/src/app/Credfeto.Keys.Server", "--self-test" ]

EXPOSE 8080

ENTRYPOINT [ "/usr/src/app/Credfeto.Keys.Server" ]

# Perform a healthcheck. Note that ECS ignores this, so this is for local development
HEALTHCHECK --interval=5s --timeout=2s --retries=3 --start-period=5s CMD [ "/usr/src/app/Credfeto.Keys.Server", "--health-check", "http://127.0.0.1:8080/ping?source=docker" ]
