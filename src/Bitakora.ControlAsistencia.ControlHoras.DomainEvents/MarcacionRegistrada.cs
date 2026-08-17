using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// HU-105: Evento de dominio que registra una marcacion ya normalizada. TipoMarcacion y
// DispositivoId son opcionales (los emite el dispositivo, no nuestro sistema).
// Issue #270: se persiste en el stream de RegistroDeMarcacionAggregateRoot y ya NO cruza el bus
// (dejo de implementar IPrivateEvent). El contrato de bus es RegistroDeMarcacionCreado
// (PrivateEvents.ControlHoras), empaquetado por RegistroDeMarcacionAggregateRoot.CrearRegistroDeMarcacionCreado().
// Issue #275: protegido con factory Crear() y ctores privados (patron TurnoCreado, MEF-ADR-0012).
// El evento nunca tuvo ctor privado -- #105 cito mal su propio patron de referencia; #270 saco al
// evento del bus y con eso desaparecio la razon (el ServiceBusDeserializador del consumidor) por la
// que no podia protegerse antes. Cerrado el ctor, ConfigurarSerializacion pasa de redundante a
// necesario: es la unica via de reconstruccion, y solo existe dentro del event store (CA-ADR-0025).
public sealed partial class MarcacionRegistrada
{
    public string CodigoColaborador { get; private set; }
    public DateTime TimestampNormalizado { get; private set; }
    public string? TipoMarcacion { get; private set; }
    public string? DispositivoId { get; private set; }

    // Issue #275 CA-1: constructor real privado -- solo el factory Crear lo invoca
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

    // Issue #275 CA-1: constructor vacio privado, solo para Marten/STJ via ConfigurarSerializacion
    // (que repuebla los backing fields por reflexion). El dominio nunca lo invoca.
    private MarcacionRegistrada() => CodigoColaborador = string.Empty;

    // Configuracion de serializacion STJ/Marten: permite deserializar con constructor privado
    // y propiedades con private set. Ver MEF-ADR-0012 y MarcacionRegistradaSerializacionTests.
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

    // Issue #275 CA-1/CA-2/CA-3: unica via de construccion. Trunca los segundos al minuto (floor) y
    // rechaza CodigoColaborador nulo, vacio o solo espacios en blanco.
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

    // Issue #275 CA-2: trunca (floor) el timestamp al minuto, descartando segundos y fracciones
    private static DateTime TruncarAlMinuto(DateTime timestamp) =>
        timestamp.AddTicks(-(timestamp.Ticks % TimeSpan.TicksPerMinute));
}
