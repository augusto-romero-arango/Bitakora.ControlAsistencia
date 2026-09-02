using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;

public sealed partial record UriMetadataRecursoProtegido
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido.UriMetadataRecursoProtegidoMensajes",
        typeof(UriMetadataRecursoProtegido).Assembly);

    internal static class Mensajes
    {
        public static string RecursoSinSegmentoDeRuta =>
            ResourceManager.GetString(nameof(RecursoSinSegmentoDeRuta))!;
    }
}
