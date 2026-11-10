using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Vive aqui y no en el Function App (MEF-ADR-0039 decision 5): la comparten el write-side y el worker
// de proyecciones.
public static class ConfiguracionSerializacionSedes
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

    // Vacio porque aun no hay VO con ctor privado, pero no es prescindible: lo invocan
    // ComposicionServicios y ConfiguracionMartenProjectionsSedes, asi que un VO registrado aqui lo
    // instalan ambos lados a la vez.
    public static void ConfigurarResolver(DefaultJsonTypeInfoResolver resolver)
    {
    }
}
