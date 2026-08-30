// El roundtrip usa las opciones REALES de produccion (CrearOpcionesMarten), nunca un resolver
// armado inline: ese pasaria en verde aunque el tipo no este registrado en
// ConfiguracionSerializacionControlHoras.ConfigurarResolver, que es el seam que se quiere vigilar.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class TurnoDiarioCanceladoSerializacionTests
{
    private static readonly Guid SolicitudCancelacionId =
        Guid.Parse("019600b0-0000-7000-8000-000000000010");

    private static readonly ColaboradorProgramado Colaborador = new(
        "CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";

    private static TurnoDiarioCancelado CrearEvento() =>
        TurnoDiarioCancelado.Crear(StreamId, Colaborador, Fecha, SolicitudCancelacionId);

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConTodosLosCampos()
    {
        var evento = CrearEvento();
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDiarioCancelado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.Colaborador.Should().Be(Colaborador);
        deserializado.Fecha.Should().Be(Fecha);
        deserializado.SolicitudCancelacionId.Should().Be(SolicitudCancelacionId);
    }

    // Si alguien borra la linea de registro de TurnoDiarioCancelado en ConfigurarResolver, este
    // test detecta la perdida: el tipo tiene constructores privados y propiedades con private set,
    // asi que el resolver por defecto no puede reconstruirlo.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeTurnoDiarioCancelado()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(CrearEvento(), opciones);

        var act = () => JsonSerializer.Deserialize<TurnoDiarioCancelado>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
