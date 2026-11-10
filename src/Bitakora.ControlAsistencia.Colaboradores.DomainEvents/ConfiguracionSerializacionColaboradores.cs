using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

// Vive en DomainEvents y no en el Function App (MEF-ADR-0039 decision 5): es la fuente unica que
// comparten el write-side y el worker de proyecciones; una copia divergente rompe la proyeccion en
// runtime, no en el build.
public static class ConfiguracionSerializacionColaboradores
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

    // Registra los VOs con ctor privado que viajan en eventos persistidos; los eventos son records
    // con ctor publico y STJ los reconstruye sin ayuda una vez registrados sus VOs.
    public static void ConfigurarResolver(DefaultJsonTypeInfoResolver resolver)
    {
        Identificacion.ConfigurarSerializacion(resolver);
        NombreColaborador.ConfigurarSerializacion(resolver);
        Etiqueta.ConfigurarSerializacion(resolver);
    }
}
