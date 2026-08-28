using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Issue #456: gemela de ConfiguracionSerializacionColaboradores/ConfiguracionSerializacionControlHoras/
// ConfiguracionSerializacionProgramacion. Vive en la raiz de Sedes.DomainEvents, no en
// Infraestructura/ del Function App (MEF-ADR-0039 decision 5): es la unica fuente que compartirian
// el write-side y un futuro worker de proyecciones sobre eventos persistidos de Sedes.
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

    // SedeRegistrada es un record plano (issue #456): ningun VO con ctor privado que registrar
    // todavia. El metodo existe para que los round-trip de este dominio usen siempre las mismas
    // opciones que Marten, en vez de un resolver armado inline (seccion 6d del test-writer).
    public static void ConfigurarResolver(DefaultJsonTypeInfoResolver resolver)
    {
    }
}
