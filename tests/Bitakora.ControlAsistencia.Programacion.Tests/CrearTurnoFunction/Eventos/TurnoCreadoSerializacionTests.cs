using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
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

    // Replica las opciones que Marten usa: PropertyNamingPolicy = null (PascalCase)
    private static JsonSerializerOptions CrearOpcionesMarten()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        SubFranja.ConfigurarSerializacion(resolver);
        FranjaOrdinaria.ConfigurarSerializacion(resolver);
        TurnoCreado.ConfigurarSerializacion(resolver);
        return new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            PropertyNamingPolicy = null // Marten fuerza null
        };
    }

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

    // Issue #423 CA-1: round-trip del descanso programado -- cero franjas ordinarias.
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
}
