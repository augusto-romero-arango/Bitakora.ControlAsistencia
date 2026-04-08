using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.Eventos;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction.Eventos;

/// <summary>
/// Verifica que TurnoCreado (con FranjaOrdinaria y SubFranja) sobrevive
/// un roundtrip de serializacion STJ — requerido por Marten.
/// </summary>
public class TurnoCreadoSerializacionTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000001");

    private static JsonSerializerOptions CrearOpciones()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        SubFranja.ConfigurarSerializacion(resolver);
        FranjaOrdinaria.ConfigurarSerializacion(resolver);
        return new JsonSerializerOptions { TypeInfoResolver = resolver };
    }

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoOrdinariaConDescansoYExtra()
    {
        var descanso = (new TimeOnly(10, 0), new TimeOnly(10, 15));
        var extra = (new TimeOnly(6, 0), new TimeOnly(8, 0));
        var comando = new CrearTurno(
            TurnoId, "Turno Completo",
            [new CrearTurno.Franja(
                new TimeOnly(6, 0), new TimeOnly(16, 0),
                [descanso], [extra])]);

        var evento = TurnoCreado.Crear(comando);
        var opciones = CrearOpciones();
        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoCreado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.TurnoId.Should().Be(TurnoId);
        deserializado.Nombre.Should().Be("Turno Completo");
        deserializado.FranjasOrdinarias.Should().HaveCount(1);
        deserializado.FranjasOrdinarias[0].ToString()
            .Should().Be("(06:00-16:00)[Descansos:(10:00-10:15)][Extras:(06:00-08:00)]");
    }
}
