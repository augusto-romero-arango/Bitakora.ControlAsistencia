using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class AusenciaDiariaAsignadaSerializacionTests
{
    private static readonly Guid AusenciaId = Guid.Parse("019600b0-0000-7000-8000-000000000020");
    private static readonly ColaboradorProgramado Colaborador = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";

    private static AusenciaDiariaAsignada CrearEvento() =>
        AusenciaDiariaAsignada.Crear(StreamId, Colaborador, Fecha, AusenciaId, "Vacaciones");

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConTodosLosCampos()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(CrearEvento(), opciones);
        var deserializado = JsonSerializer.Deserialize<AusenciaDiariaAsignada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.Colaborador.Should().Be(Colaborador);
        deserializado.Fecha.Should().Be(Fecha);
        deserializado.AusenciaId.Should().Be(AusenciaId);
        deserializado.Motivo.Should().Be("Vacaciones");
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeAusenciaDiariaAsignada()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(CrearEvento(), opciones);

        var act = () => JsonSerializer.Deserialize<AusenciaDiariaAsignada>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
