using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearTurno;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. La descripcion MCP de la tool y de
// sus parametros NO vive aqui: es un atributo y exige una constante de compilacion.
public partial class CrearTurnoTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.CrearTurno.CrearTurnoToolMensajes",
        typeof(CrearTurnoTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: nombre del campo obligatorio en blanco.</summary>
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        /// <summary>{0}: cuerpo de la respuesta 400/409 del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoTurnoCreado =>
            ResourceManager.GetString(nameof(ResultadoTurnoCreado))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;

        public static string FranjasJsonInvalido =>
            ResourceManager.GetString(nameof(FranjasJsonInvalido))!;

        /// <summary>{0}: posicion (desde 1) del elemento de franjas.</summary>
        public static string FranjaObligatoria =>
            ResourceManager.GetString(nameof(FranjaObligatoria))!;

        /// <summary>{0}: posicion del elemento; {1}: valor de franja recibido.</summary>
        public static string FranjaInvalida =>
            ResourceManager.GetString(nameof(FranjaInvalida))!;

        /// <summary>{0}: posicion del elemento; {1}: valor de descanso recibido.</summary>
        public static string DescansoInvalido =>
            ResourceManager.GetString(nameof(DescansoInvalido))!;

        /// <summary>{0}: posicion del elemento; {1}: valor de extra recibido.</summary>
        public static string ExtraInvalido =>
            ResourceManager.GetString(nameof(ExtraInvalido))!;

        /// <summary>{0}: codigo de sede recibido.</summary>
        public static string SedeNoExiste =>
            ResourceManager.GetString(nameof(SedeNoExiste))!;

        /// <summary>{0}: codigo de sede recibido.</summary>
        public static string SedeInactiva =>
            ResourceManager.GetString(nameof(SedeInactiva))!;
    }
}
