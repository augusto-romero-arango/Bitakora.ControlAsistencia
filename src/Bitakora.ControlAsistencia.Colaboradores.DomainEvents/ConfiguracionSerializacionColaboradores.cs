using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

// Issue #330: gemela de ConfiguracionSerializacionControlHoras/ConfiguracionSerializacionProgramacion.
//
// DESVIACION del plan del planner (documentada en el resumen de test-writer): el issue #330 sugiere
// "ConfiguracionSerializacionColaboradores en Colaboradores/Infraestructura (gemela de
// ConfiguracionSerializacionCalculoHoras)". ConfiguracionSerializacionCalculoHoras NO es el
// precedente aplicable aqui -- esa clase registra VOs de CALCULO que nunca se persisten (viven en
// Infraestructura/ del Function App a proposito, MEF-ADR-0039 seccion 5 solo mueve lo que persiste).
// Identificacion y NombreColaborador SI son payload de eventos persistidos (ColaboradorRegistrado),
// asi que el precedente real es ConfiguracionSerializacionControlHoras/ConfiguracionSerializacionProgramacion:
// ambas viven en la raiz de su {Dominio}.DomainEvents (MEF-ADR-0039 decision 5), no en el Function
// App -- es la unica fuente que el write-side Y el futuro worker de proyecciones pueden compartir.
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

    // STUB (fase roja, issue #330): intencionalmente vacio. El implementer debe invocar aqui
    // Identificacion.ConfigurarSerializacion(resolver) y NombreColaborador.ConfigurarSerializacion(
    // resolver) (VOs de #348, ya con su propio resolver implementado) -- ColaboradorRegistrado y
    // VinculacionIniciada son records con ctor publico y no declaran ConfigurarSerializacion propio,
    // asi que una vez que sus VOs anidados esten registrados aqui, STJ los reconstruye sin ayuda
    // adicional (ver comentarios de ColaboradorRegistrado.cs).
    // Ver ColaboradorRegistradoSerializacionTests: el round-trip falla mientras este metodo este
    // vacio (Identificacion/NombreColaborador tienen ctor privado, inalcanzable para STJ sin este
    // registro).
    public static void ConfigurarResolver(DefaultJsonTypeInfoResolver resolver)
    {
        // Intencionalmente vacio -- fase roja.
    }
}
