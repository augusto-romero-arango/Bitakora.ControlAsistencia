using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

// Issue #237: registro de serializacion de los VOs de calculo de ControlHoras, separado del de los
// eventos (ConfiguracionSerializacionControlHoras, en ControlHoras.DomainEvents).
//
// La particion sigue el criterio del refactor: DomainEvents registra lo que se persiste en el event
// store -- y es la lista que el worker de proyecciones puede replicar sin arrastrar el calculo de
// horas--; esta clase registra el modelo de calculo, que hoy no se persiste porque el aggregate
// recalcula DesgloseHoras en cada Apply y no hay snapshots.
//
// ComposicionServicios la invoca junto con la de eventos por dos razones: es donde vive el registro
// el dia que estos tipos si se deserialicen (un snapshot del aggregate, o un read model con el
// desglose rico), y porque el store real la necesita hoy para sostener la barrera de #232 CA-5 --
// ComposicionServiciosTests hace round-trip de IntervaloTemporal contra el ISerializer que compuso
// el contenedor, y lo usa como canario justamente porque no sobrevive STJ vanilla.
//
// Los tipos que la usan (IntervaloTemporal, Retardo) llevan ctor privado + ConfigurarSerializacion
// segun MEF-ADR-0012, asi que STJ no puede reconstruirlos sin este resolver. Alcance real: ademas de
// esos dos, cubre a IntervaloClasificado, DesgloseFranja y DesgloseHoras, que los contienen.
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
        // Issue #143: ambos alineados con MEF-ADR-0012 (ctor vacio + ConfigurarSerializacion).
        IntervaloTemporal.ConfigurarSerializacion(resolver);
        Retardo.ConfigurarSerializacion(resolver);
    }
}
