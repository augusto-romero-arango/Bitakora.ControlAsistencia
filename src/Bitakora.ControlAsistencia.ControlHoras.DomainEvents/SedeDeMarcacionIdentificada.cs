using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Issue #463: cierre del enriquecimiento coreografiado (MEF-ADR-0046) -- ControlHoras persiste el
// estampado de sede que Sedes resolvio y publico como SedeDeMarcacionResuelta (#467). Se persiste
// en el stream de ControlDiarioAggregateRoot (Id = stream ID compuesto), no cruza el bus.
// ADR-0024: evento del aggregate (categoria event-sourcing), sin marker de bus. Nombre
// deliberadamente distinto de SedeDeMarcacionResuelta (PrivateEvents.Sedes): el Function App
// referencia ambos ensamblados y nombres simples distintos evitan un using equivocado silencioso
// (mismo criterio del par MarcacionRegistrada/RegistroDeMarcacionCreado, #270).
public sealed class SedeDeMarcacionIdentificada
{
    // Id es el stream key del ControlDiario, tal como lo computa ControlDiarioAggregateRoot.ComputarStreamId.
    public string Id { get; private set; } = null!;

    // TimestampNormalizado + DispositivoId correlacionan con la marcacion ya adicionada al dia
    // (MarcacionAdicionada ya guarda ambos campos).
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

    // Configuracion de serializacion STJ/Marten: permite deserializar con constructor privado
    // y propiedades con private set. Ver MEF-ADR-0012 y SedeDeMarcacionIdentificadaSerializacionTests.
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
