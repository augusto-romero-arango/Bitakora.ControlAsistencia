using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class TurnoDiarioAsignadoSerializacionTests
{
    private static readonly Guid SolicitudId = Guid.Parse("019600b0-0000-7000-8000-000000000001");
    private static readonly ColaboradorProgramado Colaborador = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";

    // Los eventos persistidos antes de #1520 traen la clave "Jornada": STJ ignora por defecto la
    // propiedad que el tipo ya no declara, asi que esos streams se leen sin purga.
    [Fact]
    public void Deserializar_IgnoraLaJornada_CuandoElJsonPersistidoLaTrae()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var turno = new TurnoDiario("Turno Manana", [], "");
        var nodo = JsonNode.Parse(JsonSerializer.Serialize(
            new TurnoDiarioAsignado(StreamId, Colaborador, Fecha, turno, SolicitudId), opciones))!.AsObject();
        nodo["Jornada"] = JsonNode.Parse("""
            {"JornadaId":"019600b0-0000-7000-8000-0000000000a1","HorasSemanalesEnMinutos":2520,
             "TopeDiarioEnMinutos":510,"MinimoDiarioEnMinutos":240,"DiasDescansoPorSemana":1,
             "Modalidad":"Flexible","DescansoAcumulado":true,"TopeDiarioDeLeyEnMinutos":540,
             "MinimoDiarioDeLeyEnMinutos":240,"HorasSemanalesDeLeyEnMinutos":2520}
            """);

        var restaurado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(nodo.ToJsonString(), opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Id.Should().Be(StreamId);
        restaurado.InformacionColaborador.Should().Be(Colaborador);
        restaurado.Fecha.Should().Be(Fecha);
        restaurado.DetalleTurno.Nombre.Should().Be("Turno Manana");
        restaurado.SolicitudId.Should().Be(SolicitudId);
    }
}
