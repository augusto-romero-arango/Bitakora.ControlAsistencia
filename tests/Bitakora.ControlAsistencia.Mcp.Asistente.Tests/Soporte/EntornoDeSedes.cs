using System.Net;
using System.Text;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

/// <summary>Datos de prueba compartidos por los tests de la cascada de sede.</summary>
internal static class EntornoDeSedes
{
    public const string RutaMaestroDeSedes = "/api/sedes/fichas";
    public const string IdTurno = "8f14e45f-ceea-4b3c-8f0a-000000000001";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static HttpResponseMessage Respuesta(HttpStatusCode status, string cuerpo) =>
        new(status) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    /// <summary>Catalogo con un turno de una franja; sedeIdDeLaFranja null = franja sin sede prearmada.</summary>
    public static string TurnosConUnaFranja(string? sedeIdDeLaFranja) => JsonSerializer.Serialize(
        new[]
        {
            new
            {
                id = IdTurno,
                nombre = "Cocina Manana",
                esDescanso = false,
                horarioResumido = "06:00-14:00",
                franjas = new[]
                {
                    new
                    {
                        horaInicio = "06:00:00",
                        horaFin = "14:00:00",
                        diaOffsetFin = 0,
                        descansos = Array.Empty<object>(),
                        extras = Array.Empty<object>(),
                        sedeId = sedeIdDeLaFranja,
                        nombreSede = sedeIdDeLaFranja is null ? null : "Sede prearmada",
                        descripcion = ""
                    }
                },
                descripcion = "",
                completo = true
            }
        },
        Web);

    /// <summary>Maestro de sedes: SUBA activa (Sede Suba, CC-100), NORTE inactiva; FANTASMA no existe.</summary>
    public static string MaestroDeSedes() => JsonSerializer.Serialize(
        new[]
        {
            new
            {
                id = "s:suba", codigo = "SUBA", nombre = "Sede Suba", ciudad = "Bogota", direccion = "Cra 1",
                centroDeCostos = "CC-100", activa = true, dispositivos = Array.Empty<string>()
            },
            new
            {
                id = "s:norte", codigo = "NORTE", nombre = "Sede Norte", ciudad = "Bogota", direccion = "Cra 2",
                centroDeCostos = "CC-200", activa = false, dispositivos = Array.Empty<string>()
            }
        },
        Web);

    public static string Directorio(params (string Identificacion, string Codigo, string? CodigoSede)[] entradas) =>
        JsonSerializer.Serialize(
            entradas.Select(e => new
            {
                identificacion = e.Identificacion,
                nombreCompleto = $"Nombre {e.Codigo}",
                codigoColaborador = e.Codigo,
                codigoSede = e.CodigoSede,
                vigenteDesde = "2025-01-01",
                vigenteHasta = (string?)null
            }),
            Web);

    public static string Fichas(params (string Identificacion, string Codigo, string? CodigoSede)[] entradas) =>
        JsonSerializer.Serialize(
            entradas.Select(e => new
            {
                id = e.Identificacion,
                nombreCompleto = $"Nombre {e.Codigo}",
                codigoColaborador = e.Codigo,
                vigenteDesde = "2025-01-01",
                vigenteHasta = (string?)null,
                etiquetas = Array.Empty<object>(),
                codigoSede = e.CodigoSede
            }),
            Web);

    /// <summary>
    /// Sedes falsas: GET sedes/fichas = maestro completo (activas e inactivas); GET sedes/fichas/{codigo} = ficha puntual.
    /// </summary>
    public static HttpResponseMessage RespondeSedes(HttpRequestMessage request, HttpStatusCode statusMaestro = HttpStatusCode.OK)
    {
        var ruta = request.RequestUri!.AbsolutePath;
        if (ruta == RutaMaestroDeSedes)
            return Respuesta(statusMaestro, statusMaestro == HttpStatusCode.OK ? MaestroDeSedes() : "boom");

        var codigo = Uri.UnescapeDataString(ruta.Split('/').Last());
        var ficha = JsonDocument.Parse(MaestroDeSedes()).RootElement.EnumerateArray()
            .FirstOrDefault(s => s.GetProperty("codigo").GetString() == codigo);
        return ficha.ValueKind == JsonValueKind.Undefined
            ? Respuesta(HttpStatusCode.NotFound, "")
            : Respuesta(HttpStatusCode.OK, ficha.GetRawText());
    }
}
