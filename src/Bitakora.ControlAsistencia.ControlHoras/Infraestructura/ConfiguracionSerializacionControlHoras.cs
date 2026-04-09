using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

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

    public static void ConfigurarResolver(DefaultJsonTypeInfoResolver resolver)
    {
        TurnoDiarioAsignado.ConfigurarSerializacion(resolver);
    }
}
