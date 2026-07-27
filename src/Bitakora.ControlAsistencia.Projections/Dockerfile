# Build context: raiz del repo -> docker build -f src/Bitakora.ControlAsistencia.Projections/Dockerfile -t <tag> .
# Imagen base sobre runtime (no aspnet): el worker no sirve HTTP, solo hostea el daemon
# asincronico de Marten. Sin bloque EXPOSE: el Container App corre sin ingress (MEF-ADR-0034
# seccion 8) -- nadie le hace requests HTTP/TCP.

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# El worker referencia Bitakora.ControlAsistencia.ReadModels (Paso 1b, MEF-ADR-0034 seccion 5): copia tambien
# su csproj ANTES de "COPY . ." para que 'dotnet restore' resuelva el ProjectReference y para
# preservar el cache de capas (un cambio en el .cs de un read model no invalida este restore).
COPY ["src/Bitakora.ControlAsistencia.Projections/Bitakora.ControlAsistencia.Projections.csproj", "src/Bitakora.ControlAsistencia.Projections/"]
COPY ["src/Bitakora.ControlAsistencia.ReadModels/Bitakora.ControlAsistencia.ReadModels.csproj", "src/Bitakora.ControlAsistencia.ReadModels/"]
RUN dotnet restore "src/Bitakora.ControlAsistencia.Projections/Bitakora.ControlAsistencia.Projections.csproj"
COPY . .
WORKDIR "/src/src/Bitakora.ControlAsistencia.Projections"
RUN dotnet build "Bitakora.ControlAsistencia.Projections.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Bitakora.ControlAsistencia.Projections.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Bitakora.ControlAsistencia.Projections.dll"]
