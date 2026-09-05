using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

public class DiaDePlantillaSemanalQuitadoSerializacionTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000622");

    // Opciones reales de produccion: un resolver armado inline hace pasar el test con el tipo sin
    // registrar en el seam, y produccion falla.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoDatosSonValidos()
    {
        var evento = DiaDePlantillaSemanalQuitado.Crear(PlantillaId, 1, DiaSemana.Desde(7));
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DiaDePlantillaSemanalQuitado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.PlantillaId.Should().Be(PlantillaId);
        deserializado.Semana.Should().Be(1);
        deserializado.Dia.Should().BeSameAs(DiaSemana.Domingo);
    }

    [Fact]
    public void Serializar_PersisteElDiaComoSuNumeroIso_SinNombreDeEnumNiEtiquetaEnEspanol()
    {
        var evento = DiaDePlantillaSemanalQuitado.Crear(PlantillaId, 1, DiaSemana.Desde(7));
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);

        var dia = JsonDocument.Parse(json).RootElement
            .GetProperty(nameof(DiaDePlantillaSemanalQuitado.Dia));
        dia.ValueKind.Should().Be(JsonValueKind.Number);
        dia.GetInt32().Should().Be(7, "ISO 8601 numera el domingo como 7, no como el 0 de System.DayOfWeek");
        json.Should().NotContain("Sunday");
        json.Should().NotContain("domingo");
        json.Should().NotContain("Domingo");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoSemanaEsCero()
    {
        var act = () => DiaDePlantillaSemanalQuitado.Crear(PlantillaId, 0, DiaSemana.Desde(7));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{DiaDePlantillaSemanalQuitado.Mensajes.SemanaNoPositiva}*");
    }

    // Guarda del registro en ConfigurarResolver: sin el, STJ no encuentra constructor publico ni
    // parameterless. Si este test dejara de lanzar, el resolver ya no seria necesario -- no lo es.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeDiaDePlantillaSemanalQuitado()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(
            DiaDePlantillaSemanalQuitado.Crear(PlantillaId, 1, DiaSemana.Desde(7)), opciones);

        var act = () => JsonSerializer.Deserialize<DiaDePlantillaSemanalQuitado>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
