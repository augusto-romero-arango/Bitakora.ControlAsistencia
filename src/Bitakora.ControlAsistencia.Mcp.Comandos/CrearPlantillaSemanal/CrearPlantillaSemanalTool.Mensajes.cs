using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.CrearPlantillaSemanal;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. La descripcion MCP de la tool y de
// sus parametros NO vive aqui: es un atributo y exige una constante de compilacion.
public partial class CrearPlantillaSemanalTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Comandos.CrearPlantillaSemanal.CrearPlantillaSemanalToolMensajes",
        typeof(CrearPlantillaSemanalTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: nombre del campo obligatorio en blanco.</summary>
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        /// <summary>{0}: valor de semanas recibido.</summary>
        public static string SemanasFueraDeRango =>
            ResourceManager.GetString(nameof(SemanasFueraDeRango))!;

        public static string DiasJsonInvalido =>
            ResourceManager.GetString(nameof(DiasJsonInvalido))!;

        public static string DiasVacio =>
            ResourceManager.GetString(nameof(DiasVacio))!;

        /// <summary>{0}: valor de dia recibido en la entrada.</summary>
        public static string DiaDesconocido =>
            ResourceManager.GetString(nameof(DiaDesconocido))!;

        /// <summary>{0}: semana de la entrada; {1}: numero de semanas de la plantilla.</summary>
        public static string SemanaFueraDeRango =>
            ResourceManager.GetString(nameof(SemanaFueraDeRango))!;

        /// <summary>{0}: semana duplicada; {1}: dia duplicado (numero ISO).</summary>
        public static string DiaDuplicado =>
            ResourceManager.GetString(nameof(DiaDuplicado))!;

        /// <summary>{0}: semana de la entrada; {1}: dia de la entrada.</summary>
        public static string TurnoObligatorioEnEntrada =>
            ResourceManager.GetString(nameof(TurnoObligatorioEnEntrada))!;

        /// <summary>{0}: nombres de turno que faltan; {1}: nombres disponibles en el catalogo.</summary>
        public static string TurnosNoExisten =>
            ResourceManager.GetString(nameof(TurnosNoExisten))!;

        /// <summary>{0}: cuerpo de la respuesta 400/409 del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoPlantillaCreada =>
            ResourceManager.GetString(nameof(ResultadoPlantillaCreada))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
