using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// HU-105: Evento privado que registra una marcacion normalizada
// El timestamp se trunca al minuto (floor) antes de emitir
// CA-2: TimestampNormalizado = Timestamp truncado al minuto
// CA-3: TipoMarcacion y DispositivoId son opcionales (nullable)
public sealed class MarcacionRegistrada : IPrivateEvent
{
    public string EmpleadoId { get; private set; } = null!;
    public DateTime TimestampNormalizado { get; private set; }
    public string? TipoMarcacion { get; private set; }
    public string? DispositivoId { get; private set; }

    public MarcacionRegistrada(
        string empleadoId,
        DateTime timestampNormalizado,
        string? tipoMarcacion,
        string? dispositivoId)
    {
        EmpleadoId = empleadoId;
        TimestampNormalizado = timestampNormalizado;
        TipoMarcacion = tipoMarcacion;
        DispositivoId = dispositivoId;
    }

    // Constructor privado para Marten/serializacion
    private MarcacionRegistrada() { }

    // Configuracion de serializacion STJ/Marten: permite deserializar con constructor privado
    // y propiedades con private set. Ver ADR-0013 y MarcacionRegistradaSerializacionTests.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(MarcacionRegistrada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(MarcacionRegistrada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (MarcacionRegistrada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(MarcacionRegistrada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
