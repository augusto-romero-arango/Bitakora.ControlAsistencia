using System.Text.Json;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

public static class CicloDeTurnosFalso
{
    public const string PlantillaId = "01a07000-1000-7000-9000-000000000001";
    public const string PlantillaIdInexistente = "01a07000-1000-7000-9000-0000000000ff";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static string TurnoDelDia(int dia) => $"8f14e45f-ceea-4b3c-8f0a-00000000000{dia}";

    public static string Plantilla(
        string nombre = "Ciclo A", int dias = 3, bool completa = true, int? diaRetirado = null, int? diaIncompleto = null) =>
        JsonSerializer.Serialize(
            new
            {
                id = PlantillaId,
                nombre,
                diasDelCiclo = dias,
                completa,
                dias = Enumerable.Range(1, dias).Select(d => new
                {
                    dia = d,
                    turno = new
                    {
                        id = TurnoDelDia(d), nombre = $"Turno {d}", descripcion = "(06:00-14:00)",
                        completo = d != diaIncompleto, retirado = d == diaRetirado
                    }
                })
            },
            Web);

    public static string Turnos(string? sedeIdDeLaFranja, int dias = 8) => JsonSerializer.Serialize(
        new
        {
            elementos = Enumerable.Range(1, dias).Select(d => new
            {
                id = TurnoDelDia(d),
                nombre = $"Turno {d}",
                esDescanso = false,
                horarioResumido = "06:00-14:00",
                franjas = new[]
                {
                    new
                    {
                        horaInicio = "06:00:00", horaFin = "14:00:00", diaOffsetFin = 0,
                        descansos = Array.Empty<object>(), extras = Array.Empty<object>(),
                        sedeId = sedeIdDeLaFranja, nombreSede = (string?)null, descripcion = ""
                    }
                },
                descripcion = "",
                completo = true
            })
        },
        Web);
}
