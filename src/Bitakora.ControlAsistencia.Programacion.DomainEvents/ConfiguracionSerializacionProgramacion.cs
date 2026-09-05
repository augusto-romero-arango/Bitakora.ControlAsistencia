using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #237: gemela de ConfiguracionSerializacionControlHoras. Antes esta lista vivia inline en
// ComposicionServicios de la Function App, asi que el worker de proyecciones no tenia como
// replicarla. Ahora el ensamblado que aloja los tipos ricos aloja tambien su registro, y
// cualquier proceso que lea estos streams -- write-side o read-side -- invoca la misma lista.
public static class ConfiguracionSerializacionProgramacion
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

    // Los tres tipos ricos de Programacion: ctor privado + propiedades con private set, que STJ
    // solo puede reconstruir con el resolver custom (MEF-ADR-0012). Van de mas interno a mas
    // externo porque SubFranja es hija de FranjaOrdinaria y esta es payload de TurnoCreado.
    // ProgramacionTurnoSolicitada no entra: su ctor publico es el unico, asi que STJ lo resuelve
    // sin ayuda y no declara ConfigurarSerializacion.
    public static void ConfigurarResolver(DefaultJsonTypeInfoResolver resolver)
    {
        SubFranja.ConfigurarSerializacion(resolver);
        FranjaOrdinaria.ConfigurarSerializacion(resolver);
        TurnoCreado.ConfigurarSerializacion(resolver);
        TurnoRetirado.ConfigurarSerializacion(resolver);
        FranjaAgregada.ConfigurarSerializacion(resolver);
        DescansoAgregado.ConfigurarSerializacion(resolver);
        ExtraAgregado.ConfigurarSerializacion(resolver);
    }
}
