FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY Kontainr.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Run as a non-root user. Note that reaching the Docker socket still requires the
# container to be in a group that can read it — pass the host's docker group with
# `--group-add $(getent group docker | cut -d: -f3)`, or run with `--user root` if
# your host does not have one.
RUN useradd --uid 10001 --create-home --shell /usr/sbin/nologin kontainr \
    && mkdir -p /app/data \
    && chown -R kontainr:kontainr /app/data \
    && chmod 700 /app/data
USER 10001

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV KONTAINR_DATA=/app/data
VOLUME /app/data
ENTRYPOINT ["dotnet", "Kontainr.dll"]
