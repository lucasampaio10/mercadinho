# Multi-stage build: compila com o SDK completo, roda com a imagem de runtime
# (bem menor) — o Render só recebe o resultado do publish, não o SDK inteiro.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia só os .csproj primeiro para o `dotnet restore` cachear entre builds
# enquanto só o código-fonte muda (o caso comum).
COPY PDV.sln .
COPY src/PDV.Domain/PDV.Domain.csproj src/PDV.Domain/
COPY src/PDV.Application/PDV.Application.csproj src/PDV.Application/
COPY src/PDV.Infrastructure/PDV.Infrastructure.csproj src/PDV.Infrastructure/
COPY src/PDV.API/PDV.API.csproj src/PDV.API/
COPY tests/PDV.Tests/PDV.Tests.csproj tests/PDV.Tests/
RUN dotnet restore PDV.sln

COPY . .
RUN dotnet publish src/PDV.API/PDV.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "PDV.API.dll"]
