FROM mcr.microsoft.com/devcontainers/dotnet:1-8.0

# Fix Yarn GPG repository signature error during apt-get update in devcontainer feature installation
RUN rm -f /etc/apt/sources.list.d/yarn.list
