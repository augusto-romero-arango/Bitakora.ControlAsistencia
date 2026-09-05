using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.AsignarSedeFranja;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. La descripcion MCP de la tool y de
// sus parametros NO vive aqui: es un atributo y exige una constante de compilacion.
public partial class AsignarSedeFranjaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Comandos.AsignarSedeFranja.AsignarSedeFranjaToolMensajes",
        typeof(AsignarSedeFranjaTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: nombre del campo obligatorio en blanco.</summary>
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        /// <summary>{0}: nombre del campo (franja); {1}: valor recibido.</summary>
        public static string HoraInvalida =>
            ResourceManager.GetString(nameof(HoraInvalida))!;

        /// <summary>{0}: nombre de turno recibido; {1}: nombres disponibles en el catalogo.</summary>
        public static string TurnoNoExiste =>
            ResourceManager.GetString(nameof(TurnoNoExiste))!;

        /// <summary>{0}: codigo de sede recibido.</summary>
        public static string SedeNoExiste =>
            ResourceManager.GetString(nameof(SedeNoExiste))!;

        /// <summary>{0}: codigo de sede recibido.</summary>
        public static string SedeInactiva =>
            ResourceManager.GetString(nameof(SedeInactiva))!;

        /// <summary>{0}: cuerpo de la respuesta 400/404/409/5xx del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoSedeAsignada =>
            ResourceManager.GetString(nameof(ResultadoSedeAsignada))!;

        public static string ResultadoSedeRetirada =>
            ResourceManager.GetString(nameof(ResultadoSedeRetirada))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
