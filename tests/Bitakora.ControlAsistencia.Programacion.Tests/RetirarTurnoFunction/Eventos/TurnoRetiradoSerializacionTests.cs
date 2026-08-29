// Issue #500: round-trip de serializacion de TurnoRetirado (seccion 6d del harness)

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.RetirarTurnoFunction.Eventos;

// Round-trip con las opciones REALES de Marten del dominio: un resolver armado inline haria pasar
// el test aunque TurnoRetirado no este registrado en ConfiguracionSerializacionProgramacion.
public class TurnoRetiradoSerializacionTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000500");

    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = TurnoRetirado.Crear(TurnoId);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<TurnoRetirado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.TurnoId.Should().Be(TurnoId);
    }

    // CA-regresion: si nadie registra TurnoRetirado.ConfigurarSerializacion en
    // ConfiguracionSerializacionProgramacion.ConfigurarResolver, el ctor privado impide a STJ
    // reconstruirlo -- este test hace visible el olvido.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeTurnoRetirado()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var json = JsonSerializer.Serialize(TurnoRetirado.Crear(TurnoId), opciones);

        var act = () => JsonSerializer.Deserialize<TurnoRetirado>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
