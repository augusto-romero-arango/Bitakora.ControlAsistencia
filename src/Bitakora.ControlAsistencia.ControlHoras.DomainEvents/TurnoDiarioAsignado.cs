using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Evento de event sourcing que registra la asignacion de un turno diario al ControlDiario.
/// Se persiste en el stream de ControlDiarioAggregateRoot y no cruza el bus.
/// ADR-0024: evento del aggregate (categoria event-sourcing), sin marker de bus.
/// </summary>
// HU-12: evento de event sourcing del aggregate ControlDiario
// CA-5: contiene InformacionEmpleado, Fecha, DetalleTurno y SolicitudId (trazabilidad)
// Issue #322: InformacionEmpleado y DetalleTurno dejan de ser tipos de PublicEvents/PrivateEvents
// -- ahora son ColaboradorProgramado y TurnoDiario, propios de este ensamblado (payload por rol,
// CA-ADR-0029 decision #5). Issue #340: ese payload paso de llamarse Empleado a
// ColaboradorProgramado (termino proscrito por #330). Los NOMBRES de estas propiedades NO cambian
// (son las claves JSON persistidas en mt_events); solo cambian los TIPOS. MEF-ADR-0036: el alias
// del evento no se toca -- deriva del nombre de clase del EVENTO, que no se renombra.
public sealed class TurnoDiarioAsignado
{
    public string Id { get; private set; } = null!;
    public ColaboradorProgramado InformacionEmpleado { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public TurnoDiario DetalleTurno { get; private set; } = null!;
    public Guid SolicitudId { get; private set; }

    public TurnoDiarioAsignado(
        string id,
        ColaboradorProgramado informacionEmpleado,
        DateOnly fecha,
        TurnoDiario detalleTurno,
        Guid solicitudId)
    {
        Id = id;
        InformacionEmpleado = informacionEmpleado;
        Fecha = fecha;
        DetalleTurno = detalleTurno;
        SolicitudId = solicitudId;
    }

    // Constructor para Marten/serializacion
    private TurnoDiarioAsignado() { }

    // Configuracion de serializacion STJ/Marten: permite deserializar con constructor privado
    // y propiedades con private set. Ver ADR-0013 y TurnoCreadoSerializacionTests.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(TurnoDiarioAsignado)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(TurnoDiarioAsignado)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (TurnoDiarioAsignado)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(TurnoDiarioAsignado).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
