using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

public class ProgramacionTurnoAgregadaAlBorradorSerializacionTests
{
    private static readonly Guid SolicitudId = Guid.Parse("019600a0-0000-7000-8000-000000001405");
    private static readonly Guid JornadaId = Guid.Parse("018e4c1a-4f2b-7000-8000-00000000861a");

    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var turno = new TurnoProgramado(
            "Turno Manana",
            new List<FranjaProgramada>
            {
                new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                    [new SubFranjaProgramada(new TimeOnly(12, 0), new TimeOnly(12, 30), 0, 0, "(12:00-12:30)")],
                    [], "(06:00-14:00)[Descansos:(12:00-12:30)]")
            }.AsReadOnly(),
            "Turno Manana (06:00-14:00)");
        var jornada = new JornadaProgramada(
            JornadaId,
            LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1));
        var evento = new ProgramacionTurnoAgregadaAlBorrador(
            SolicitudId,
            new ColaboradorProgramado("CC-12345678", "E001", "Juan Perez"),
            [new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 29)],
            turno,
            new SedeProgramada("SEDE-01", "Sede Principal", "CC-01"),
            jornada);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoAgregadaAlBorrador>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.SolicitudId.Should().Be(SolicitudId);
        restaurado.Colaborador.Should().Be(new ColaboradorProgramado("CC-12345678", "E001", "Juan Perez"));
        restaurado.Fechas.Should().Equal(new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 29));
        restaurado.DetalleTurno.Should().Be(turno);
        restaurado.DetalleTurno.FranjasOrdinarias[0].Descansos.Should().HaveCount(1);
        restaurado.Sede.Should().Be(new SedeProgramada("SEDE-01", "Sede Principal", "CC-01"));
        restaurado.Jornada.Should().Be(jornada);
    }

    [Fact]
    public void RoundTrip_ConservaSedeNula_CuandoLaSolicitudNoTraeSede()
    {
        var evento = new ProgramacionTurnoAgregadaAlBorrador(
            SolicitudId,
            new ColaboradorProgramado("CC-12345678", "E001", "Juan Perez"),
            [new DateOnly(2026, 10, 28)],
            new TurnoProgramado("T", new List<FranjaProgramada>().AsReadOnly(), "T"),
            null,
            new JornadaProgramada(
                JornadaId,
                LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1)));
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoAgregadaAlBorrador>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Sede.Should().BeNull();
        restaurado.Fechas.Should().Equal(new DateOnly(2026, 10, 28));
    }

    [Fact]
    public void RoundTrip_ConservaLaEstampaDeLaJornada_ConDatosCompletos()
    {
        var jornada = new JornadaProgramada(
            JornadaId,
            LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1),
            ModalidadJornada.Flexible, true, new FichaDeLey(540, 240, 2520));
        var evento = new ProgramacionTurnoAgregadaAlBorrador(
            SolicitudId,
            new ColaboradorProgramado("CC-12345678", "E001", "Juan Perez"),
            [new DateOnly(2026, 10, 28)],
            new TurnoProgramado("T", new List<FranjaProgramada>().AsReadOnly(), "T"),
            null,
            jornada);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoAgregadaAlBorrador>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Jornada.Modalidad.Should().Be(ModalidadJornada.Flexible);
        restaurado.Jornada.DescansoAcumulado.Should().BeTrue();
        restaurado.Jornada.Ficha.Should().Be(new FichaDeLey(540, 240, 2520));
    }

    [Fact]
    public void Deserializar_DejaModalidadNulaMarcaFalsaYFichaNula_CuandoLaJornadaPersistidaNoTraeLosCamposNuevos()
    {
        var evento = new ProgramacionTurnoAgregadaAlBorrador(
            SolicitudId,
            new ColaboradorProgramado("CC-12345678", "E001", "Juan Perez"),
            [new DateOnly(2026, 10, 28)],
            new TurnoProgramado("T", new List<FranjaProgramada>().AsReadOnly(), "T"),
            null,
            new JornadaProgramada(
                JornadaId,
                LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1)));
        var opciones = CrearOpcionesMarten();
        var nodo = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(evento, opciones))!.AsObject();
        var jornadaNodo = nodo["Jornada"]!.AsObject();
        jornadaNodo.Remove("Modalidad");
        jornadaNodo.Remove("DescansoAcumulado");
        jornadaNodo.Remove("Ficha");

        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoAgregadaAlBorrador>(nodo.ToJsonString(), opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Jornada.JornadaId.Should().Be(JornadaId);
        restaurado.Jornada.Modalidad.Should().BeNull();
        restaurado.Jornada.DescansoAcumulado.Should().BeFalse();
        restaurado.Jornada.Ficha.Should().BeNull();
    }
}
