using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

public class TurnoDePlantillaSemanalSincronizadoSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000882");
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000883");

    private static readonly Turno TurnoDeTrabajo = Turno.Crear(
        "Turno Mixto", false,
        [
            FranjaOrdinaria.Crear(
                new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                [SubFranja.Crear(new TimeOnly(9, 0), new TimeOnly(9, 30))],
                [SubFranja.Crear(new TimeOnly(13, 0), new TimeOnly(14, 0))],
                new SedeProgramada("s:001", "Sede Principal", "CC-100"))
        ]);

    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoDatosSonValidos()
    {
        var evento = TurnoDePlantillaSemanalSincronizado.Crear(PlantillaId, TurnoId, TurnoDeTrabajo, 7, false);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDePlantillaSemanalSincronizado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.PlantillaId.Should().Be(PlantillaId);
        deserializado.TurnoId.Should().Be(TurnoId);
        deserializado.Turno.Should().Be(TurnoDeTrabajo);
        deserializado.VersionTurno.Should().Be(7);
        deserializado.Retirado.Should().BeFalse();
    }

    [Fact]
    public void Deserializar_ReconstruyeLaMarcaDeRetirado_CuandoElTurnoEsDescansoRetirado()
    {
        var descanso = Turno.Crear("Descanso Compensatorio", true, []);
        var evento = TurnoDePlantillaSemanalSincronizado.Crear(PlantillaId, TurnoId, descanso, 8, true);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDePlantillaSemanalSincronizado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Turno.Should().Be(descanso);
        deserializado.Turno.EsDescanso().Should().BeTrue();
        deserializado.VersionTurno.Should().Be(8);
        deserializado.Retirado.Should().BeTrue();
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeTurnoDePlantillaSemanalSincronizado()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            TurnoDePlantillaSemanalSincronizado.Crear(PlantillaId, TurnoId, TurnoDeTrabajo, 7, false), opciones);

        var act = () => JsonSerializer.Deserialize<TurnoDePlantillaSemanalSincronizado>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
