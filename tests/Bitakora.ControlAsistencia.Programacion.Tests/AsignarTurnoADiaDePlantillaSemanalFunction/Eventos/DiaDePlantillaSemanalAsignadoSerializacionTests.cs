// Issue #621 CA-2: round-trip STJ con las opciones reales de Marten, y el contrato de persistencia
// del dia (numero ISO entero, nunca el nombre del enum de .NET ni una etiqueta en espanol).

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarTurnoADiaDePlantillaSemanalFunction.Eventos;

public class DiaDePlantillaSemanalAsignadoSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000621");
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000701");

    // Opciones reales de produccion: un resolver armado inline hace pasar el test con el tipo sin
    // registrar en el seam, y produccion falla.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoDatosSonValidos()
    {
        var evento = DiaDePlantillaSemanalAsignado.Crear(PlantillaId, 2, DiaSemana.Desde(5), TurnoId);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DiaDePlantillaSemanalAsignado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.PlantillaId.Should().Be(PlantillaId);
        deserializado.Semana.Should().Be(2);
        deserializado.Dia.Should().BeSameAs(DiaSemana.Viernes);
        deserializado.TurnoId.Should().Be(TurnoId);
    }

    // CA-2: el numero ISO persiste como entero -- nunca "Friday" (nombre del enum DayOfWeek de
    // .NET) ni "viernes"/"Viernes" (etiqueta en espanol, que ademas ni siquiera existe todavia:
    // DiaSemana no tiene ToString() localizado en este issue).
    [Fact]
    public void Serializar_PersisteElDiaComoSuNumeroIso_SinNombreDeEnumNiEtiquetaEnEspanol()
    {
        var evento = DiaDePlantillaSemanalAsignado.Crear(PlantillaId, 2, DiaSemana.Desde(5), TurnoId);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);

        json.Should().Contain("5");
        json.Should().NotContain("Friday");
        json.Should().NotContain("viernes");
        json.Should().NotContain("Viernes");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoSemanaEsCero()
    {
        var act = () => DiaDePlantillaSemanalAsignado.Crear(PlantillaId, 0, DiaSemana.Desde(5), TurnoId);

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
            DiaDePlantillaSemanalAsignado.Crear(PlantillaId, 2, DiaSemana.Desde(5), TurnoId), opciones);

        var act = () => JsonSerializer.Deserialize<DiaDePlantillaSemanalAsignado>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
