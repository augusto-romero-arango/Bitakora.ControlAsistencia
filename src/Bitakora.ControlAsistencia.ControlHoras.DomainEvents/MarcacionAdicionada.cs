using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Se persiste en el stream de ControlDiarioAggregateRoot; no cruza el bus.
public sealed class MarcacionAdicionada
{
    // Id es el stream key del ControlDiario, tal como lo computa ControlDiarioAggregateRoot.ComputarStreamId.
    public string Id { get; private set; } = null!;
    public string CodigoColaborador { get; private set; } = null!;

    // Ya viene truncado al minuto desde RegistrarMarcacion.
    public DateTime TimestampNormalizado { get; private set; }

    public string? TipoMarcacion { get; private set; }
    public string? DispositivoId { get; private set; }

    public MarcacionAdicionada(
        string id,
        string codigoColaborador,
        DateTime timestampNormalizado,
        string? tipoMarcacion,
        string? dispositivoId)
    {
        Id = id;
        CodigoColaborador = codigoColaborador;
        TimestampNormalizado = timestampNormalizado;
        TipoMarcacion = tipoMarcacion;
        DispositivoId = dispositivoId;
    }

    // Solo para Marten/STJ: ConfigurarSerializacion lo invoca por reflexion.
    private MarcacionAdicionada() { }

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(MarcacionAdicionada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(MarcacionAdicionada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (MarcacionAdicionada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(MarcacionAdicionada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
