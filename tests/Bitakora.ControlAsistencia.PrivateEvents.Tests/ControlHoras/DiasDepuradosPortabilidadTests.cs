using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.ControlHoras;

public class DiasDepuradosPortabilidadTests
{
    private static JsonSerializerOptions CrearOpcionesDelBus() => new(JsonSerializerDefaults.Web);

    private static DiaDepurado Dia(DateOnly fecha, string nombreTurno) => new(
        "EMP-001", fecha, null, nombreTurno,
        [new FranjaDepurada(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
            fecha.ToDateTime(new TimeOnly(7, 0)), fecha.ToDateTime(new TimeOnly(15, 0)), false,
            "001", "Sede Principal", "CC-100")],
        [new MarcacionDelDia(fecha.ToDateTime(new TimeOnly(7, 0)), "ENTRADA", "001", "Sede Principal", "CC-100")],
        new HorasDiscriminadas(new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m }, ["06:00-14:00: Ordinaria diurna"]));

    [Fact]
    public void RoundTrip_PreservaTodosLosCampos_ConSerializadorPorDefectoDelBus()
    {
        var evento = new DiasDepurados("EMP-001",
            [Dia(new DateOnly(2026, 3, 15), "Turno Manana"), Dia(new DateOnly(2026, 3, 16), "Turno Tarde")]);
        var opciones = CrearOpcionesDelBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DiasDepurados>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.CodigoColaborador.Should().Be("EMP-001");
        restaurado.Dias.Should().Equal(evento.Dias);
    }
}
