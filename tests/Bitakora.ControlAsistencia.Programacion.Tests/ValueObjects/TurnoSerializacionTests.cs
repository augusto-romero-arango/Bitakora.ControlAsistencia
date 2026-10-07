using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class TurnoSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    private static Turno RoundTrip(Turno turno)
    {
        var opciones = CrearOpciones();
        var json = JsonSerializer.Serialize(turno, opciones);
        return JsonSerializer.Deserialize<Turno>(json, opciones)!;
    }

    [Fact]
    public void RoundTrip_ReconstruyeTurno_ConDescansosYExtras()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))
            .ConDescanso(new TimeOnly(10, 0), new TimeOnly(10, 30))
            .ConExtra(new TimeOnly(12, 0), new TimeOnly(14, 0));
        var turno = Turno.Crear("Turno Manana", false, [franja]);

        var restaurado = RoundTrip(turno);

        restaurado.Should().Be(turno);
        restaurado.ToString().Should().Be(turno.ToString());
        restaurado.EstaCompleto().Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_ReconstruyeTurno_CuandoEsDescanso()
    {
        var restaurado = RoundTrip(Turno.Crear("Libre", true, []));

        restaurado.EstaCompleto().Should().BeTrue();
        restaurado.ToString().Should().Be($"Libre {Turno.Mensajes.LabelDescanso}");
    }

    [Fact]
    public void RoundTrip_ReconstruyeTurno_CuandoEstaIncompleto()
    {
        var restaurado = RoundTrip(Turno.Crear("Nuevo", false, []));

        restaurado.EstaCompleto().Should().BeFalse();
        restaurado.ToString().Should().Be($"Nuevo {Turno.Mensajes.LabelIncompleto}");
    }

    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeTurno()
    {
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

        var act = () => JsonSerializer.Deserialize<Turno>(
            JsonSerializer.Serialize(Turno.Crear("Nuevo", false, []), CrearOpciones()), opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
