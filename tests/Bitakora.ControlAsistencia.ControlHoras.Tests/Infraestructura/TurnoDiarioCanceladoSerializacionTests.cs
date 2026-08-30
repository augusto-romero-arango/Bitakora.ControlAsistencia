// Issue #499, regla 16 / seccion 6d: todo evento persistido en Marten debe tener test de
// serializacion roundtrip con las opciones REALES de produccion (CrearOpcionesMarten), nunca un
// resolver armado inline -- ese pasaria en verde aunque el tipo no este registrado en
// ConfiguracionSerializacionControlHoras.ConfigurarResolver (el seam real que el implementer debe
// tocar). TurnoDiarioCancelado es simetrico de TurnoDiarioAsignado: constructor privado y
// propiedades con private set -- necesita el mismo ConfigurarSerializacion (ver
// TurnoDiarioAsignadoSerializacionTests, el precedente exacto de este archivo).

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
        new(StreamId, Colaborador, Fecha, SolicitudCancelacionId);

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

    // CA-regresion (6d): si alguien borra la linea de registro de TurnoDiarioCancelado en
    // ConfigurarResolver, este test detecta la perdida -- el tipo tiene constructor privado y
    // propiedades con private set, asi que el resolver por defecto no puede reconstruirlo.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeTurnoDiarioCancelado()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(CrearEvento(), opciones);

        var act = () => JsonSerializer.Deserialize<TurnoDiarioCancelado>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
