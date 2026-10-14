using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.DesactivarJornada;

public partial class DesactivarJornadaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.DesactivarJornada.DesactivarJornadaToolMensajes",
        typeof(DesactivarJornadaTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: jornada_id recibido.</summary>
        public static string JornadaIdInvalido =>
            ResourceManager.GetString(nameof(JornadaIdInvalido))!;

        public static string JornadaEnUsoUnColaborador =>
            ResourceManager.GetString(nameof(JornadaEnUsoUnColaborador))!;

        /// <summary>{0}: cantidad de colaboradores vigentes (mayor que 1).</summary>
        public static string JornadaEnUsoVariosColaboradores =>
            ResourceManager.GetString(nameof(JornadaEnUsoVariosColaboradores))!;

        public static string UnaTerminacionRegistrada =>
            ResourceManager.GetString(nameof(UnaTerminacionRegistrada))!;

        /// <summary>{0}: cantidad de colaboradores con terminacion registrada (mayor que 1).</summary>
        public static string VariasTerminacionesRegistradas =>
            ResourceManager.GetString(nameof(VariasTerminacionesRegistradas))!;

        /// <summary>{0}: cuerpo de la respuesta 404/409/5xx del dominio o fallo de lectura de fichas.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string JornadaPredeterminada =>
            ResourceManager.GetString(nameof(JornadaPredeterminada))!;

        /// <summary>{0}: jornada_id pedido.</summary>
        public static string JornadaNoExiste =>
            ResourceManager.GetString(nameof(JornadaNoExiste))!;

        public static string ResultadoJornadaDesactivada =>
            ResourceManager.GetString(nameof(ResultadoJornadaDesactivada))!;
    }
}
