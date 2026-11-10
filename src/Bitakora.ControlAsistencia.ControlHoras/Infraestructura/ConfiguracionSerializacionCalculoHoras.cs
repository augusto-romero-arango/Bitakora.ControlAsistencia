using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

// Separado de ConfiguracionSerializacionControlHoras (DomainEvents): aquella registra lo que se
// persiste en el event store, que el worker de proyecciones puede replicar sin arrastrar el calculo
// de horas; esta registra el modelo de calculo, que hoy no se persiste porque el aggregate
// recalcula DesgloseHoras en cada Apply y no hay snapshots.
//
// ComposicionServicios la invoca igual: ComposicionServiciosTests hace round-trip de
// IntervaloTemporal contra el ISerializer del contenedor y lo usa como canario porque no sobrevive
// STJ vanilla.
//
// IntervaloTemporal y Retardo llevan ctor privado (MEF-ADR-0012); el registro cubre tambien a
// IntervaloClasificado, DesgloseFranja y DesgloseHoras, que los contienen.
public static class ConfiguracionSerializacionCalculoHoras
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
        IntervaloTemporal.ConfigurarSerializacion(resolver);
        Retardo.ConfigurarSerializacion(resolver);
    }
}
