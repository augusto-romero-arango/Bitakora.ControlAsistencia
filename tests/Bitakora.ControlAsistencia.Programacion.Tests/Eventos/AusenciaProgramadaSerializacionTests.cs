using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

// Opciones REALES de Marten del dominio: un resolver armado inline haria pasar el test aunque
// MotivoAusencia no estuviera registrado en ConfiguracionSerializacionProgramacion.
public class AusenciaProgramadaSerializacionTests
{
    private static readonly Guid AusenciaId = Guid.Parse("019600a0-0000-7000-8000-000000000743");

    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new AusenciaProgramada(
            AusenciaId,
            new ColaboradorProgramado("CC-12345678", "E001", "Ana Maria Gomez"),
            new DateOnly(2026, 10, 5),
            new DateOnly(2026, 10, 7),
            MotivoAusencia.IncapacidadMedica);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<AusenciaProgramada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.AusenciaId.Should().Be(AusenciaId);
        restaurado.Colaborador.Should().Be(new ColaboradorProgramado("CC-12345678", "E001", "Ana Maria Gomez"));
        restaurado.FechaInicio.Should().Be(new DateOnly(2026, 10, 5));
        restaurado.FechaFin.Should().Be(new DateOnly(2026, 10, 7));
        restaurado.Motivo.ToString().Should().Be("IncapacidadMedica");
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeMotivoAusencia()
    {
        var evento = new AusenciaProgramada(
            AusenciaId,
            new ColaboradorProgramado("CC-12345678", "E001", "Ana Maria Gomez"),
            new DateOnly(2026, 10, 5),
            new DateOnly(2026, 10, 7),
            MotivoAusencia.Vacaciones);
        var json = JsonSerializer.Serialize(evento, CrearOpcionesMarten());
        var vacias = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

        var act = () => JsonSerializer.Deserialize<AusenciaProgramada>(json, vacias);

        act.Should().Throw<NotSupportedException>();
    }
}
