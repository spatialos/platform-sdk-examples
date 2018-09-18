FROM microsoft/dotnet:2.0-sdk AS build
COPY blank_project blank_project
COPY csharp csharp

WORKDIR /csharp
RUN dotnet restore --force
RUN dotnet build
