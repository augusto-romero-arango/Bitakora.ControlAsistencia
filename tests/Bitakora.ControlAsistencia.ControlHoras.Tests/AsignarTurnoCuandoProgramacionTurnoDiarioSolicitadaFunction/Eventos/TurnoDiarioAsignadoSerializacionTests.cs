// HU-12: Asignar turno diario al control cuando se solicita programacion

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

/// <summary>
/// Verifica que TurnoDiarioAsignado sobrevive un roundtrip de serializacion STJ.
/// Requerido porque Marten usa STJ con PropertyNamingPolicy=null (PascalCase)
/// y el evento tiene constructor privado y propiedades con private set.
/// Ver ADR-0013 y feedback de memoria: ConfigurarSerializacion es obligatorio.
///
/// Issue #322: InformacionEmpleado y DetalleTurno cambian de tipo (Empleado/TurnoDiario, propios
/// de ControlHoras.DomainEvents) pero NO de nombre de propiedad -- son las claves JSON que ya
/// estan persistidas en mt_events.
/// </summary>
public class TurnoDiarioAsignadoSerializacionTests
{
    private static readonly Guid SolicitudId =
        Guid.Parse("019600b0-0000-7000-8000-000000000001");

    private static readonly Empleado EmpleadoDePrueba = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new DateOnly(2026, 3, 15);

    // Issue #288: Descripcion (dato derivado, texto del formato tecnico) es irrelevante para este
    // round-trip -> placeholder no vacio para verificar tambien que sobrevive la serializacion.
    private static readonly TurnoDiario TurnoDiarioDePrueba = new(
        "Turno Manana",
        [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "(08:00-16:00)")],
        "Turno Manana (08:00-16:00)");

    private static readonly string StreamId = $"{EmpleadoDePrueba.EmpleadoId}:{Fecha:yyyy-MM-dd}";

    // Regla 16: todo evento persistido en Marten debe tener test de serializacion roundtrip
    // Verifica con datos reales y completos (no listas vacias para VOs anidados)
    [Fact]
    public void Deserializar_ReconstruyeEvento_ConTodosLosCampos()
    {
        var evento = new TurnoDiarioAsignado(StreamId, EmpleadoDePrueba, Fecha, TurnoDiarioDePrueba, SolicitudId);
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.SolicitudId.Should().Be(SolicitudId);
        deserializado.Fecha.Should().Be(Fecha);
        deserializado.InformacionEmpleado.EmpleadoId.Should().Be(EmpleadoDePrueba.EmpleadoId);
        deserializado.InformacionEmpleado.Nombres.Should().Be(EmpleadoDePrueba.Nombres);
        deserializado.DetalleTurno.Nombre.Should().Be(TurnoDiarioDePrueba.Nombre);
        deserializado.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);
        deserializado.DetalleTurno.FranjasOrdinarias[0].HoraInicio
            .Should().Be(new TimeOnly(8, 0));

        // Issue #288 CA-7: Descripcion (dato derivado persistido) sobrevive el round-trip,
        // tanto a nivel turno como a nivel de cada franja ordinaria.
        deserializado.DetalleTurno.Descripcion.Should().Be(TurnoDiarioDePrueba.Descripcion);
        deserializado.DetalleTurno.FranjasOrdinarias[0].Descripcion
            .Should().Be(TurnoDiarioDePrueba.FranjasOrdinarias[0].Descripcion);
    }

    // Issue #322 CA-2 / Nota de seguridad de datos (critica): JSON con la FORMA EXACTA que Marten
    // ya escribio en mt_events ANTES de este issue (PascalCase, PropertyNamingPolicy=null). Los
    // nombres de propiedad (InformacionEmpleado, Fecha, DetalleTurno, SolicitudId) no cambian --
    // solo los tipos anidados (Empleado/TurnoDiario en vez de InformacionEmpleado/DetalleTurno de
    // PublicEvents/PrivateEvents). Este round-trip demuestra que un evento ya persistido se relee
    // identico, sin ninguna migracion de datos (MEF-ADR-0036: el alias del evento no se toca).
    //
    // La comparacion es por igualdad estructural completa (.Should().Be() sobre el objeto
    // compuesto, no solo sus campos escalares): asi el test tambien exige que TurnoDiario/
    // FranjaProgramada repliquen la semantica de igualdad de los originales (CA-1), no solo su
    // forma de campos.
    [Fact]
    public void Deserializar_ReconstruyeIdentico_DesdeElJsonConLaFormaYaPersistida()
    {
        const string jsonPersistidoHoy = """
            {
              "Id": "EMP-001:2026-03-15",
              "InformacionEmpleado": {
                "EmpleadoId": "EMP-001",
                "TipoIdentificacion": "CC",
                "NumeroIdentificacion": "1234567890",
                "Nombres": "Luis Augusto",
                "Apellidos": "Barreto"
              },
              "Fecha": "2026-03-15",
              "DetalleTurno": {
                "Nombre": "Turno Manana",
                "FranjasOrdinarias": [
                  {
                    "HoraInicio": "08:00:00",
                    "HoraFin": "16:00:00",
                    "DiaOffsetFin": 0,
                    "Descansos": [],
                    "Extras": [],
                    "Descripcion": "(08:00-16:00)"
                  }
                ],
                "Descripcion": "Turno Manana (08:00-16:00)"
              },
              "SolicitudId": "019600b0-0000-7000-8000-000000000001"
            }
            """;
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(jsonPersistidoHoy, opciones);

        var empleadoEsperado = new Empleado("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");
        var turnoEsperado = new TurnoDiario(
            "Turno Manana",
            [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "(08:00-16:00)")],
            "Turno Manana (08:00-16:00)");

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be("EMP-001:2026-03-15");
        deserializado.SolicitudId.Should().Be(Guid.Parse("019600b0-0000-7000-8000-000000000001"));
        deserializado.Fecha.Should().Be(new DateOnly(2026, 3, 15));
        deserializado.InformacionEmpleado.Should().Be(empleadoEsperado);
        deserializado.DetalleTurno.Should().Be(turnoEsperado);
    }
}
