// Issue #319 CA-2: guardrail decisivo de seguridad de datos. ProgramacionTurnoSolicitada SE
// PERSISTE (stream de SolicitudProgramacionAggregateRoot); sus propiedades Empleado/DetalleTurno
// cambiaron de tipo (InformacionEmpleado/DetalleTurno, PublicEvents/PrivateEvents) a los records
// propios del dominio (Empleado/TurnoProgramado, Programacion.DomainEvents) -- MISMOS nombres de
// propiedad y de campo anidado, tipos nuevos.
//
// Este test construye el JSON con la FORMA en la que Marten ya persistio este evento antes del
// cambio de tipos (mismos nombres de propiedad, con los tipos viejos) y lo deserializa contra el
// tipo NUEVO usando las mismas opciones que Marten usa en produccion (CrearOpcionesMarten()). STJ
// no persiste $type para records/clases anidadas -- ningun dato existente se pierde. Sin este
// guardrail, el cambio de tipo podria romper en silencio la lectura de streams ya escritos.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoFunction.Eventos;

public class ProgramacionTurnoSolicitadaSerializacionTests
{
    private static readonly Guid Id = Guid.Parse("019600a0-0000-7000-8000-000000000050");
    private static readonly DateOnly Fecha = new(2026, 4, 7);

    // Forma EXACTA en la que Marten persistio este evento ANTES de este issue: mismos nombres de
    // propiedad ("Empleado", "Fechas", "DetalleTurno" y sus anidados) que hoy tipan con
    // InformacionEmpleado/DetalleTurno (PublicEvents/PrivateEvents). Un tipo anonimo local basta
    // para reproducir la FORMA sin necesitar referenciar esos ensamblados: lo unico que importa
    // para el guardrail es que las claves JSON coincidan.
    private static string JsonConFormaPersistidaAntesDelCambioDeTipos()
    {
        var forma = new
        {
            Id,
            Empleado = new
            {
                EmpleadoId = "E001",
                TipoIdentificacion = "CC",
                NumeroIdentificacion = "12345678",
                Nombres = "Juan",
                Apellidos = "Perez"
            },
            Fechas = new[] { Fecha },
            DetalleTurno = new
            {
                Nombre = "Turno Manana",
                FranjasOrdinarias = new[]
                {
                    new
                    {
                        HoraInicio = new TimeOnly(6, 0),
                        HoraFin = new TimeOnly(14, 0),
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>(),
                        Descripcion = "(06:00-14:00)"
                    }
                },
                Descripcion = "Turno Manana (06:00-14:00)"
            }
        };

        return JsonSerializer.Serialize(forma, new JsonSerializerOptions { PropertyNamingPolicy = null });
    }

    [Fact]
    public void Deserializar_ReconstruyeEventoIdentico_ConLaFormaPersistidaAntesDelCambioDeTipos()
    {
        var json = JsonConFormaPersistidaAntesDelCambioDeTipos();
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var evento = JsonSerializer.Deserialize<ProgramacionTurnoSolicitada>(json, opciones);

        evento.Should().NotBeNull();
        evento!.Id.Should().Be(Id);
        evento.Empleado.Should().Be(new Empleado("E001", "CC", "12345678", "Juan", "Perez"));
        evento.Fechas.Should().Equal(Fecha);
        evento.DetalleTurno.Should().Be(new TurnoProgramado(
            "Turno Manana",
            new List<FranjaProgramada>
            {
                new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "(06:00-14:00)")
            }.AsReadOnly(),
            "Turno Manana (06:00-14:00)"));
    }
}
