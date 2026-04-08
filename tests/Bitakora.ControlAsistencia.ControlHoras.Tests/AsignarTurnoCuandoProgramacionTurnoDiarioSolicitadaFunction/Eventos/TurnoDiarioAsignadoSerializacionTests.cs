// HU-12: Asignar turno diario al control cuando se solicita programacion

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

/// <summary>
/// Verifica que TurnoDiarioAsignado sobrevive un roundtrip de serializacion STJ.
/// Requerido porque Marten usa STJ con PropertyNamingPolicy=null (PascalCase)
/// y el evento tiene constructor privado y propiedades con private set.
/// Ver ADR-0013 y feedback de memoria: ConfigurarSerializacion es obligatorio.
/// </summary>
public class TurnoDiarioAsignadoSerializacionTests
{
    private static readonly Guid SolicitudId =
        Guid.Parse("019600b0-0000-7000-8000-000000000001");

    private static readonly InformacionEmpleado Empleado = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new DateOnly(2026, 3, 15);

    private static readonly DetalleTurno DetalleTurnoTest = new(
        "Turno Manana",
        [new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [])]);

    // Replica las opciones que Marten usa: PropertyNamingPolicy = null (PascalCase)
    private static JsonSerializerOptions CrearOpcionesMarten()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        TurnoDiarioAsignado.ConfigurarSerializacion(resolver);
        return new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            PropertyNamingPolicy = null // Marten fuerza null
        };
    }

    // Regla 16: todo evento persistido en Marten debe tener test de serializacion roundtrip
    // Verifica con datos reales y completos (no listas vacias para VOs anidados)
    [Fact]
    public void Deserializar_ReconstruyeEvento_ConTodosLosCampos()
    {
        var evento = new TurnoDiarioAsignado(Empleado, Fecha, DetalleTurnoTest, SolicitudId);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.SolicitudId.Should().Be(SolicitudId);
        deserializado.Fecha.Should().Be(Fecha);
        deserializado.InformacionEmpleado.EmpleadoId.Should().Be(Empleado.EmpleadoId);
        deserializado.InformacionEmpleado.Nombres.Should().Be(Empleado.Nombres);
        deserializado.DetalleTurno.Nombre.Should().Be(DetalleTurnoTest.Nombre);
        deserializado.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);
        deserializado.DetalleTurno.FranjasOrdinarias[0].HoraInicio
            .Should().Be(new TimeOnly(8, 0));
    }
}
