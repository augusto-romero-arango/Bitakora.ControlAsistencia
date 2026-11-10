using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Asignacion de un turno diario al ControlDiario. Se persiste en el stream de
/// ControlDiarioAggregateRoot y no cruza el bus.
/// </summary>
// El alias del evento deriva del nombre de esta clase, que no se renombra (MEF-ADR-0036).
public sealed class TurnoDiarioAsignado
{
    public string Id { get; private set; } = null!;
    public ColaboradorProgramado InformacionColaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public TurnoDiario DetalleTurno { get; private set; } = null!;
    public Guid SolicitudId { get; private set; }
    public JornadaProgramada? Jornada { get; private set; }

    public TurnoDiarioAsignado(
        string id,
        ColaboradorProgramado informacionColaborador,
        DateOnly fecha,
        TurnoDiario detalleTurno,
        Guid solicitudId,
        JornadaProgramada? jornada = null)
    {
        Id = id;
        InformacionColaborador = informacionColaborador;
        Fecha = fecha;
        DetalleTurno = detalleTurno;
        SolicitudId = solicitudId;
        Jornada = jornada;
    }

    // Solo para Marten/STJ: ConfigurarSerializacion lo invoca por reflexion.
    private TurnoDiarioAsignado() { }

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
