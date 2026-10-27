using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

public class TurnoDePlantillaDeTurnosSincronizadoSerializacionTests
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
        var evento = TurnoDePlantillaDeTurnosSincronizado.Crear(PlantillaId, TurnoId, TurnoDeTrabajo, 7, false);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDePlantillaDeTurnosSincronizado>(json, opciones);

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
        var evento = TurnoDePlantillaDeTurnosSincronizado.Crear(PlantillaId, TurnoId, descanso, 8, true);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDePlantillaDeTurnosSincronizado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Turno.Should().Be(descanso);
        deserializado.Turno.EsDescanso().Should().BeTrue();
        deserializado.VersionTurno.Should().Be(8);
        deserializado.Retirado.Should().BeTrue();
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeTurnoDePlantillaDeTurnosSincronizado()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            TurnoDePlantillaDeTurnosSincronizado.Crear(PlantillaId, TurnoId, TurnoDeTrabajo, 7, false), opciones);

        var act = () => JsonSerializer.Deserialize<TurnoDePlantillaDeTurnosSincronizado>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }

    private static readonly Turno TurnoDeHorarioAbierto = Turno.Crear(
        "Turno Abierto", false,
        [FranjaOrdinaria.Crear(new TimeOnly(4, 0), new TimeOnly(10, 0))],
        esHorarioAbierto: true);

    [Fact]
    public void RoundTrip_PreservaLaMarcaDeHorarioAbierto_CuandoElTurnoEsDeHorarioAbierto()
    {
        var evento = TurnoDePlantillaDeTurnosSincronizado.Crear(PlantillaId, TurnoId, TurnoDeHorarioAbierto, 7, false);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDePlantillaDeTurnosSincronizado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Turno.EsHorarioAbierto().Should().BeTrue();
    }

    [Fact]
    public void Deserializar_AsumeHorarioAbiertoFalse_CuandoLaCopiaDelTurnoNoTraeLaClave()
    {
        var evento = TurnoDePlantillaDeTurnosSincronizado.Crear(PlantillaId, TurnoId, TurnoDeHorarioAbierto, 7, false);
        var opciones = CrearOpcionesMarten();
        var json = TurnoDeHorarioAbiertoEventos.QuitarClave(
            JsonSerializer.Serialize(evento, opciones), "esHorarioAbierto");

        var deserializado = JsonSerializer.Deserialize<TurnoDePlantillaDeTurnosSincronizado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Turno.EsHorarioAbierto().Should().BeFalse();
    }
}
