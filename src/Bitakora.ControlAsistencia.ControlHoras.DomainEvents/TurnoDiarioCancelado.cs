using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Registra la cancelacion del turno diario asignado a un ControlDiario. Se persiste en el stream
/// de ControlDiarioAggregateRoot y no cruza el bus. Simetrico de TurnoDiarioAsignado.
/// </summary>
// Forma canonica de MEF-ADR-0012: ctor parametrizado privado + ctor vacio privado + factory Crear.
// Que ningun ctor sea publico es lo que hace fallar a STJ vanilla sin ConfigurarSerializacion --
// la regresion que vigila Deserializar_Falla_CuandoResolverNoTieneRegistroDeTurnoDiarioCancelado.
public sealed class TurnoDiarioCancelado
{
    public string Id { get; private set; } = null!;
    public ColaboradorProgramado Colaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public Guid SolicitudCancelacionId { get; private set; }

    private TurnoDiarioCancelado(
        string id,
        ColaboradorProgramado colaborador,
        DateOnly fecha,
        Guid solicitudCancelacionId)
    {
        Id = id;
        Colaborador = colaborador;
        Fecha = fecha;
        SolicitudCancelacionId = solicitudCancelacionId;
    }

    // Constructor para Marten/serializacion
    private TurnoDiarioCancelado() { }

    public static TurnoDiarioCancelado Crear(
        string id, ColaboradorProgramado colaborador, DateOnly fecha, Guid solicitudCancelacionId) =>
        new(id, colaborador, fecha, solicitudCancelacionId);

    // Configuracion de serializacion STJ/Marten: permite deserializar con constructor privado
    // y propiedades con private set. Simetrico de TurnoDiarioAsignado.ConfigurarSerializacion.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(TurnoDiarioCancelado)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(TurnoDiarioCancelado)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (TurnoDiarioCancelado)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(TurnoDiarioCancelado).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
