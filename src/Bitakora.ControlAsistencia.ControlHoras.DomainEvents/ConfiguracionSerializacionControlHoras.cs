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

    // Solo los tipos que se persisten en el event store de control_horas. Los VOs de calculo, que
    // nunca se persisten, se registran en ControlHoras.Infraestructura.ConfiguracionSerializacionCalculoHoras.
    public static void ConfigurarResolver(DefaultJsonTypeInfoResolver resolver)
    {
        TurnoDiarioAsignado.ConfigurarSerializacion(resolver);
        MarcacionRegistrada.ConfigurarSerializacion(resolver);
        MarcacionAdicionada.ConfigurarSerializacion(resolver);
        DepuracionDiaRecibida.ConfigurarSerializacion(resolver);
        SedeDeMarcacionIdentificada.ConfigurarSerializacion(resolver);
        DiaAprobado.ConfigurarSerializacion(resolver);
        DepuracionPosAprobacionRecibida.ConfigurarSerializacion(resolver);
        TurnoDiarioCancelado.ConfigurarSerializacion(resolver);
        AusenciaDiariaAsignada.ConfigurarSerializacion(resolver);
        CancelacionAusenciaDiariaRegistrada.ConfigurarSerializacion(resolver);
        CoberturaVecinaActualizada.ConfigurarSerializacion(resolver);
        CierreDeLaVisperaActualizado.ConfigurarSerializacion(resolver);
        AmbiguedadDeMarcacionesResuelta.ConfigurarSerializacion(resolver);
        ResolucionDeAmbiguedadDescartada.ConfigurarSerializacion(resolver);
    }
}
