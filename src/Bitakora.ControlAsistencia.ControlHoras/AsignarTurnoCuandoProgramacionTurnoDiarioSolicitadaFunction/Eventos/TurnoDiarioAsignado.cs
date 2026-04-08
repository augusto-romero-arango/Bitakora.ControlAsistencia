using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

/// <summary>
/// Evento privado que registra la asignacion de un turno diario al ControlDiario.
/// Se persiste en el stream de ControlDiarioAggregateRoot.
/// No se publica al Service Bus.
/// </summary>
// HU-12: evento de event sourcing del aggregate ControlDiario
// CA-5: contiene InformacionEmpleado, Fecha, DetalleTurno y SolicitudId (trazabilidad)
public sealed class TurnoDiarioAsignado : IPrivateEvent
{
    public InformacionEmpleado InformacionEmpleado { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public DetalleTurno DetalleTurno { get; private set; } = null!;
    public Guid SolicitudId { get; private set; }

    public TurnoDiarioAsignado(
        InformacionEmpleado informacionEmpleado,
        DateOnly fecha,
        DetalleTurno detalleTurno,
        Guid solicitudId)
    {
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
