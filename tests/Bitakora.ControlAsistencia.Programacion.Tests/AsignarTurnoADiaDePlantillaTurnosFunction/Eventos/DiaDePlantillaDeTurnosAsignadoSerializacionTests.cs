using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarTurnoADiaDePlantillaTurnosFunction.Eventos;

public class DiaDePlantillaDeTurnosAsignadoSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001281");
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000701");

    private static readonly Turno TurnoDeTrabajo = Turno.Crear(
        "Turno Manana", false,
        [
            FranjaOrdinaria.Crear(
                new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                [SubFranja.Crear(new TimeOnly(9, 0), new TimeOnly(9, 30))],
                [SubFranja.Crear(new TimeOnly(13, 0), new TimeOnly(14, 0))])
        ]);

    private static readonly Turno TurnoDeHorarioAbierto = Turno.Crear(
        "Turno Abierto", false,
        [FranjaOrdinaria.Crear(new TimeOnly(4, 0), new TimeOnly(10, 0))],
        esHorarioAbierto: true);

    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoDatosSonValidos()
    {
        var evento = DiaDePlantillaDeTurnosAsignado.Crear(PlantillaId, 6, TurnoId, TurnoDeTrabajo, 7);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DiaDePlantillaDeTurnosAsignado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.PlantillaId.Should().Be(PlantillaId);
        deserializado.Dia.Should().Be(6);
        deserializado.TurnoId.Should().Be(TurnoId);
        deserializado.Turno.Should().Be(TurnoDeTrabajo);
        deserializado.VersionTurno.Should().Be(7);
    }

    [Fact]
    public void RoundTrip_PreservaLaMarcaDeHorarioAbierto_CuandoElTurnoEsDeHorarioAbierto()
    {
        var evento = DiaDePlantillaDeTurnosAsignado.Crear(PlantillaId, 2, TurnoId, TurnoDeHorarioAbierto, 7);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DiaDePlantillaDeTurnosAsignado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Turno.EsHorarioAbierto().Should().BeTrue();
        deserializado.Dia.Should().Be(2);
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoDiaEsCero()
    {
        var act = () => DiaDePlantillaDeTurnosAsignado.Crear(PlantillaId, 0, TurnoId, TurnoDeTrabajo, 7);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{DiaDePlantillaDeTurnosAsignado.Mensajes.DiaNoPositivo}*");
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeDiaDePlantillaDeTurnosAsignado()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            DiaDePlantillaDeTurnosAsignado.Crear(PlantillaId, 2, TurnoId, TurnoDeTrabajo, 7), opciones);

        var act = () => JsonSerializer.Deserialize<DiaDePlantillaDeTurnosAsignado>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
