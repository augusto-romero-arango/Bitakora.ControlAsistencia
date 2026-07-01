using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.Eventos;

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
        // Issue #143: IntervaloTemporal alineado con ADR-0015 (ctor vacio + ConfigurarSerializacion).
        IntervaloTemporal.ConfigurarSerializacion(resolver);
        TurnoDiarioAsignado.ConfigurarSerializacion(resolver);
        MarcacionRegistrada.ConfigurarSerializacion(resolver);
        MarcacionAdicionada.ConfigurarSerializacion(resolver);
        Retardo.ConfigurarSerializacion(resolver);
    }
}
