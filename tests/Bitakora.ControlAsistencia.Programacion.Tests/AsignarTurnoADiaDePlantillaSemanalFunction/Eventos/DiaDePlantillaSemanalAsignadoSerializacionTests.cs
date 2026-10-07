using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarTurnoADiaDePlantillaSemanalFunction.Eventos;

public class DiaDePlantillaSemanalAsignadoSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000621");
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000701");

    private static readonly Turno TurnoDeTrabajo = Turno.Crear(
        "Turno Manana", false,
        [
            FranjaOrdinaria.Crear(
                new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                [SubFranja.Crear(new TimeOnly(9, 0), new TimeOnly(9, 30))],
                [SubFranja.Crear(new TimeOnly(13, 0), new TimeOnly(14, 0))])
        ]);

    private static readonly Turno TurnoDeDescanso = Turno.Crear("Descanso Compensatorio", true, []);

    // Opciones reales de produccion: un resolver armado inline hace pasar el test con el tipo sin
    // registrar en el seam, y produccion falla.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoDatosSonValidos()
    {
        var evento = DiaDePlantillaSemanalAsignado.Crear(PlantillaId, 2, DiaSemana.Desde(5), TurnoId, TurnoDeTrabajo, 7);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DiaDePlantillaSemanalAsignado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.PlantillaId.Should().Be(PlantillaId);
        deserializado.Semana.Should().Be(2);
        deserializado.Dia.Should().BeSameAs(DiaSemana.Viernes);
        deserializado.TurnoId.Should().Be(TurnoId);
        deserializado.Turno.Should().Be(TurnoDeTrabajo);
        deserializado.VersionTurno.Should().Be(7);
    }

    // CA-5: turno con descanso y extra sobrevive el round-trip con la copia completa.
    [Fact]
    public void Deserializar_ReconstruyeLaCopiaDelTurnoDeDescanso_CuandoElTurnoEsDescanso()
    {
        var evento = DiaDePlantillaSemanalAsignado.Crear(
            PlantillaId, 1, DiaSemana.Desde(7), TurnoId, TurnoDeDescanso, 2);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DiaDePlantillaSemanalAsignado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Turno.Should().Be(TurnoDeDescanso);
        deserializado.Turno.EsDescanso().Should().BeTrue();
        deserializado.VersionTurno.Should().Be(2);
        deserializado.Dia.Should().BeSameAs(DiaSemana.Domingo);
    }

    [Fact]
    public void Serializar_PersisteElDiaComoSuNumeroIso_SinNombreDeEnumNiEtiquetaEnEspanol()
    {
        var evento = DiaDePlantillaSemanalAsignado.Crear(PlantillaId, 2, DiaSemana.Desde(5), TurnoId, TurnoDeTrabajo, 7);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);

        var dia = JsonDocument.Parse(json).RootElement
            .GetProperty(nameof(DiaDePlantillaSemanalAsignado.Dia));
        dia.ValueKind.Should().Be(JsonValueKind.Number);
        dia.GetInt32().Should().Be(5, "ISO 8601 numera el viernes como 5, no como el 4 de System.DayOfWeek");
        json.Should().NotContain("Friday");
        json.Should().NotContain("viernes");
        json.Should().NotContain("Viernes");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoSemanaEsCero()
    {
        var act = () => DiaDePlantillaSemanalAsignado.Crear(PlantillaId, 0, DiaSemana.Desde(5), TurnoId, TurnoDeTrabajo, 7);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{DiaDePlantillaSemanalAsignado.Mensajes.SemanaNoPositiva}*");
    }

    // Guarda del registro en ConfigurarResolver: sin el, STJ no encuentra constructor publico ni
    // parameterless. Si este test dejara de lanzar, el resolver ya no seria necesario -- no lo es.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeDiaDePlantillaSemanalAsignado()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            DiaDePlantillaSemanalAsignado.Crear(PlantillaId, 2, DiaSemana.Desde(5), TurnoId, TurnoDeTrabajo, 7), opciones);

        var act = () => JsonSerializer.Deserialize<DiaDePlantillaSemanalAsignado>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
