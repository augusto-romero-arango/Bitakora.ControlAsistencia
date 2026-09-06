using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.AsignarTurnoADia;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. La descripcion MCP de la tool y de
// sus parametros NO vive aqui: es un atributo y exige una constante de compilacion.
public partial class AsignarTurnoADiaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Comandos.AsignarTurnoADia.AsignarTurnoADiaToolMensajes",
        typeof(AsignarTurnoADiaTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: nombre del campo obligatorio en blanco.</summary>
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        /// <summary>{0}: valor de semana recibido.</summary>
        public static string SemanaInvalida =>
            ResourceManager.GetString(nameof(SemanaInvalida))!;

        /// <summary>{0}: valor de dia recibido.</summary>
        public static string DiaDesconocido =>
            ResourceManager.GetString(nameof(DiaDesconocido))!;

        /// <summary>{0}: nombre de plantilla recibido; {1}: nombres disponibles en el catalogo.</summary>
        public static string PlantillaNoExiste =>
            ResourceManager.GetString(nameof(PlantillaNoExiste))!;

        /// <summary>{0}: nombre de turno recibido; {1}: nombres disponibles en el catalogo.</summary>
        public static string TurnoNoExiste =>
            ResourceManager.GetString(nameof(TurnoNoExiste))!;

        /// <summary>{0}: cuerpo de la respuesta 404/409/5xx del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoTurnoAsignado =>
            ResourceManager.GetString(nameof(ResultadoTurnoAsignado))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
