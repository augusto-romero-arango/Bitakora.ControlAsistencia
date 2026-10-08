using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class TurnoDiarioAsignadoJornadaSerializacionTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600b0-0000-7000-8000-0000000000a1");
    private static readonly Guid SolicitudId = Guid.Parse("019600b0-0000-7000-8000-000000000001");
    private static readonly ColaboradorProgramado Colaborador = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";

    private static TurnoDiarioAsignado Crear(JornadaProgramada? jornada) =>
        new(StreamId, Colaborador, Fecha, new TurnoDiario("Turno Manana", [], ""), SolicitudId, jornada);

    [Fact]
    public void RoundTrip_PreservaLaJornada_ConDatosCompletos()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(Crear(new JornadaProgramada(JornadaId, 2520, 510, 240, 1)), opciones);
        var restaurado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Jornada.Should().NotBeNull();
        restaurado.Jornada!.JornadaId.Should().Be(JornadaId);
        restaurado.Jornada.HorasSemanalesEnMinutos.Should().Be(2520);
        restaurado.Jornada.TopeDiarioEnMinutos.Should().Be(510);
        restaurado.Jornada.MinimoDiarioEnMinutos.Should().Be(240);
        restaurado.Jornada.DiasDescansoPorSemana.Should().Be(1);
    }

    [Fact]
    public void Deserializar_DejaJornadaNula_CuandoElJsonNoTraeLaClave()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var nodo = JsonNode.Parse(JsonSerializer.Serialize(Crear(null), opciones))!.AsObject();
        nodo.Remove("Jornada");

        var restaurado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(nodo.ToJsonString(), opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Jornada.Should().BeNull();
        restaurado.SolicitudId.Should().Be(SolicitudId);
    }
}
