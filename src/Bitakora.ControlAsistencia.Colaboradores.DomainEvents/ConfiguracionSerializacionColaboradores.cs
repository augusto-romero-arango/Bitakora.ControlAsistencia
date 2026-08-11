using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

// Issue #330: gemela de ConfiguracionSerializacionControlHoras/ConfiguracionSerializacionProgramacion.
//
// Vive en la raiz de Colaboradores.DomainEvents, no en Infraestructura/ del Function App
// (MEF-ADR-0039 decision 5): Identificacion y NombreColaborador son payload de un evento PERSISTIDO
// (ColaboradorRegistrado), y esta es la unica fuente que pueden compartir los dos procesos que leen
// ese evento -- el write-side (ComposicionServicios.AgregarServiciosColaboradores) y el worker de
// proyecciones (ConfiguracionMartenProjectionsColaboradores). Fuente unica, nunca una copia
// (MEF-ADR-0029); una divergencia entre ambos lados rompe la proyeccion en runtime, no en el build.
// Contraejemplo: ConfiguracionSerializacionCalculoHoras si vive en el Function App de ControlHoras,
// porque registra VOs de calculo que nunca se persisten y el worker no necesita.
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

    // Registra los VOs con ctor privado que aparecen como payload de eventos persistidos --
    // ColaboradorRegistrado y VinculacionIniciada son records con ctor publico y no declaran
    // ConfigurarSerializacion propio, asi que una vez que sus VOs anidados esten registrados aqui,
    // STJ los reconstruye sin ayuda adicional (ver comentarios de ColaboradorRegistrado.cs).
    public static void ConfigurarResolver(DefaultJsonTypeInfoResolver resolver)
    {
        Identificacion.ConfigurarSerializacion(resolver);
        NombreColaborador.ConfigurarSerializacion(resolver);
    }
}
