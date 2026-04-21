using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.Eventos;

// HU-106: Evento privado que registra la adicion de una marcacion al ControlDiario
// Se persiste en el stream de ControlDiarioAggregateRoot (Id = stream ID compuesto)
// Publicado localmente via Wolverine; consumido por #107 para depuracion
public sealed class MarcacionAdicionada : IPrivateEvent
{
    // CA-7: Id es el stream ID del ControlDiario: "{EmpleadoId}:{Fecha:yyyy-MM-dd}"
    public string Id { get; private set; } = null!;
    public string EmpleadoId { get; private set; } = null!;

    // TimestampNormalizado viene ya truncado al minuto desde RegistrarMarcacion (#105)
    public DateTime TimestampNormalizado { get; private set; }

    // CA-3: TipoMarcacion y DispositivoId son opcionales
    public string? TipoMarcacion { get; private set; }
    public string? DispositivoId { get; private set; }

    public MarcacionAdicionada(
        string id,
        string empleadoId,
        DateTime timestampNormalizado,
        string? tipoMarcacion,
        string? dispositivoId)
    {
        Id = id;
        EmpleadoId = empleadoId;
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
