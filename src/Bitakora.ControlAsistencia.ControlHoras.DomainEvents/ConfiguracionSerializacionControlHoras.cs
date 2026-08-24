using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public static class ConfiguracionSerializacionControlHoras
{
    public static JsonSerializerOptions CrearOpcionesMarten()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        ConfigurarResolver(resolver);
        return new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            PropertyNamingPolicy = null
        };
    }

    // Issue #237: solo los tipos que se persisten en el event store de control_horas. Los VOs de
    // calculo (IntervaloTemporal, Retardo) salieron de esta lista: no son payload de ningun evento
    // -- el unico que los contiene, DesgloseHoras, es estado del aggregate que se recalcula en cada
    // Apply y nunca se persiste. Su registro vive ahora en
    // ControlHoras.Infraestructura.ConfiguracionSerializacionCalculoHoras.
    public static void ConfigurarResolver(DefaultJsonTypeInfoResolver resolver)
    {
        TurnoDiarioAsignado.ConfigurarSerializacion(resolver);
        MarcacionRegistrada.ConfigurarSerializacion(resolver);
        MarcacionAdicionada.ConfigurarSerializacion(resolver);
        DepuracionDiaRecibida.ConfigurarSerializacion(resolver);
    }
}
