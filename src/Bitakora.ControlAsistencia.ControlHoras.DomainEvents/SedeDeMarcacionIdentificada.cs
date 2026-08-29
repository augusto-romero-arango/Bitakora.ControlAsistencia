using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Evento de event sourcing persistido en el stream de ControlDiarioAggregateRoot; no cruza el bus,
// sin marker (MEF-ADR-0024). El nombre simple difiere a proposito del evento de bus que lo origina
// (SedeDeMarcacionResuelta, PrivateEvents.Sedes): el Function App ve ambos ensamblados y dos
// homonimos harian que un using equivocado compile y resuelva mal en silencio (MEF-ADR-0039 #6).
public sealed class SedeDeMarcacionIdentificada
{
    // Id es el stream key del ControlDiario, tal como lo computa ControlDiarioAggregateRoot.ComputarStreamId.
    public string Id { get; private set; } = null!;

    // Correlacion con la marcacion ya adicionada al dia: la marcacion no tiene id propio, y este par
    // es lo unico que MarcacionAdicionada tambien guarda.
    public DateTime TimestampNormalizado { get; private set; }
    public string? DispositivoId { get; private set; }

    // Estampado del momento del hecho: una modificacion posterior del maestro de sedes no lo cambia.
    public string CodigoSede { get; private set; } = null!;
    public string NombreSede { get; private set; } = null!;
    public string? CentroDeCostos { get; private set; }

    public SedeDeMarcacionIdentificada(
        string id,
        DateTime timestampNormalizado,
        string? dispositivoId,
        string codigoSede,
        string nombreSede,
        string? centroDeCostos)
    {
        Id = id;
        TimestampNormalizado = timestampNormalizado;
        DispositivoId = dispositivoId;
        CodigoSede = codigoSede;
        NombreSede = nombreSede;
        CentroDeCostos = centroDeCostos;
    }

    // Constructor privado para Marten/serializacion
    private SedeDeMarcacionIdentificada() { }

    // Deserializacion con ctor privado y propiedades private set: STJ no lo resuelve solo y Marten no
    // respeta [JsonConstructor] en ctores privados (MEF-ADR-0012).
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(SedeDeMarcacionIdentificada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(SedeDeMarcacionIdentificada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (SedeDeMarcacionIdentificada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(SedeDeMarcacionIdentificada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
