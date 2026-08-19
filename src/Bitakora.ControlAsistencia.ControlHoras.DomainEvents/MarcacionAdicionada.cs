using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// HU-106: Evento de event sourcing que registra la adicion de una marcacion al ControlDiario.
// Se persiste en el stream de ControlDiarioAggregateRoot (Id = stream ID compuesto) y no cruza el bus.
// ADR-0024: evento del aggregate (categoria event-sourcing), sin marker de bus.
public sealed class MarcacionAdicionada
{
    // CA-7: Id es el stream ID del ControlDiario: "cd:{CodigoColaborador}:{Fecha:yyyyMMdd}" (issue #420, CA-ADR-0031)
    public string Id { get; private set; } = null!;
    public string CodigoColaborador { get; private set; } = null!;

    // TimestampNormalizado viene ya truncado al minuto desde RegistrarMarcacion (#105)
    public DateTime TimestampNormalizado { get; private set; }

    // CA-3: TipoMarcacion y DispositivoId son opcionales
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

    // Constructor privado para Marten/serializacion
    private MarcacionAdicionada() { }

    // Configuracion de serializacion STJ/Marten: permite deserializar con constructor privado
    // y propiedades con private set. Ver ADR-0013 y MarcacionAdicionadaSerializacionTests.
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
