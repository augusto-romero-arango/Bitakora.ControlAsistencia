using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction.Eventos;

/// <summary>
/// Verifica que TurnoCreado (con FranjaOrdinaria y SubFranja) sobrevive
/// un roundtrip de serializacion STJ — requerido por Marten.
/// </summary>
public class TurnoCreadoSerializacionTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000001");

    // Opciones reales de produccion: un resolver armado inline hace pasar el test con el tipo sin
    // registrar en el seam, y produccion falla.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoOrdinariaConDescansoYExtra()
    {
        var descanso = (new TimeOnly(10, 0), new TimeOnly(10, 15));
        var extra = (new TimeOnly(6, 0), new TimeOnly(8, 0));
        var evento = TurnoCreado.Crear(
            TurnoId, "Turno Completo",
            [new DatosFranja(
                new TimeOnly(6, 0), new TimeOnly(16, 0),
                [descanso], [extra])]);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoCreado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Nombre.Should().Be("Turno Completo");
        deserializado.FranjasOrdinarias.Should().HaveCount(1);
        deserializado.FranjasOrdinarias[0].ToString()
            .Should().Be("(06:00-16:00)[Descansos:(10:00-10:15)][Extras:(06:00-08:00)]");
    }

    // Issue #335 CA-5: round-trip con sedes diferentes en cada franja ordinaria.
    [Fact]
    public void Deserializar_PreservaSedePorFranja_CuandoOrdinariasTraenSedesDiferentes()
    {
        var sedeManana = new SedeProgramada("SEDE-SUBA", "Suba");
        var sedeTarde = new SedeProgramada("SEDE-CHAPINERO", "Chapinero");
        var evento = TurnoCreado.Crear(
            TurnoId, "Turno Partido",
            [
                new DatosFranja(new TimeOnly(6, 0), new TimeOnly(12, 0), [], [], sedeManana),
                new DatosFranja(new TimeOnly(14, 0), new TimeOnly(18, 0), [], [], sedeTarde)
            ]);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoCreado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.FranjasOrdinarias[0].ToDetalle().Sede.Should().Be(sedeManana);
        deserializado.FranjasOrdinarias[1].ToDetalle().Sede.Should().Be(sedeTarde);
    }

    // Issue #335 CA-4: retrocompatibilidad -- una franja sin sede prearmada no agrega la clave
    // "sede" al JSON persistido (equivalente al JSON escrito antes de este issue), y deserializa
    // con Sede null.
    [Fact]
    public void Deserializar_DejaSedeNull_CuandoFranjaNoTraeSede()
    {
        var evento = TurnoCreado.Crear(
            TurnoId, "Turno Completo",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(16, 0), [], [])]);

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoCreado>(json, opciones);

        json.Should().NotContain("\"sede\"");
        deserializado.Should().NotBeNull();
        deserializado!.FranjasOrdinarias[0].ToDetalle().Sede.Should().BeNull();
    }

    [Fact]
    public void Deserializar_ReconstruyeEventoConFranjasVacias_CuandoEsDescanso()
    {
        var evento = TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");

        var opciones = CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoCreado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Nombre.Should().Be("Descanso Compensatorio");
        deserializado.FranjasOrdinarias.Should().BeEmpty();
    }

    // ---------- CA-3: EsDescanso sobrevive el roundtrip en las dos variantes ----------

    [Fact]
    public void RoundTrip_PreservaEsDescansoTrue_CuandoEventoEsDescanso()
    {
        var evento = TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoCreado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.EsDescanso.Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_PreservaEsDescansoFalse_CuandoEventoTieneAlMenosUnaFranja()
    {
        var evento = TurnoCreado.Crear(
            TurnoId, "Turno Completo",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(16, 0), [], [])]);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoCreado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.EsDescanso.Should().BeFalse();
    }

    // Pin explicito de la decision de compatibilidad: un JSON legado (streams ya escritos antes de
    // este campo) no trae la clave EsDescanso -- debe deserializar false, no lanzar.
    [Fact]
    public void Deserializar_AsumeEsDescansoFalse_CuandoJsonLegadoNoTraeLaClave()
    {
        var opciones = CrearOpcionesMarten();
        var json = $$"""
            {"TurnoId":"{{TurnoId}}","Nombre":"Turno Legado","FranjasOrdinarias":[]}
            """;

        var deserializado = JsonSerializer.Deserialize<TurnoCreado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.EsDescanso.Should().BeFalse();
    }
}
