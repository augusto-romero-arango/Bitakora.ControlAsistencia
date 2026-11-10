using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Marcacion ya normalizada. TipoMarcacion y DispositivoId son opcionales porque los emite el
// dispositivo, no nuestro sistema. Se persiste en el stream de RegistroDeMarcacionAggregateRoot y
// no cruza el bus: el contrato de bus es RegistroDeMarcacionCreado, que empaqueta
// RegistroDeMarcacionAggregateRoot.CrearRegistroDeMarcacionCreado().
public sealed partial class MarcacionRegistrada
{
    public string CodigoColaborador { get; private set; }
    public DateTime TimestampNormalizado { get; private set; }
    public string? TipoMarcacion { get; private set; }
    public string? DispositivoId { get; private set; }

    private MarcacionRegistrada(
        string codigoColaborador,
        DateTime timestampNormalizado,
        string? tipoMarcacion,
        string? dispositivoId)
    {
        CodigoColaborador = codigoColaborador;
        TimestampNormalizado = timestampNormalizado;
        TipoMarcacion = tipoMarcacion;
        DispositivoId = dispositivoId;
    }

    // Solo para Marten/STJ: ConfigurarSerializacion lo invoca por reflexion y repuebla los backing
    // fields. Es la unica via de reconstruccion del evento.
    private MarcacionRegistrada() => CodigoColaborador = string.Empty;

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

    // Unica via de construccion: trunca el timestamp al minuto (floor).
    public static MarcacionRegistrada Crear(
        string codigoColaborador,
        DateTime timestampCrudo,
        string? tipoMarcacion,
        string? dispositivoId)
    {
        if (string.IsNullOrWhiteSpace(codigoColaborador))
            throw new ArgumentException(Mensajes.CodigoColaboradorVacio, nameof(codigoColaborador));

        return new MarcacionRegistrada(
            codigoColaborador, TruncarAlMinuto(timestampCrudo), tipoMarcacion, dispositivoId);
    }

    private static DateTime TruncarAlMinuto(DateTime timestamp) =>
        timestamp.AddTicks(-(timestamp.Ticks % TimeSpan.TicksPerMinute));
}
