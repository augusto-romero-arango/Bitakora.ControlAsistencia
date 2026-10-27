using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RegistrarColaboradores;

public partial class RegistrarColaboradoresTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.RegistrarColaboradores.RegistrarColaboradoresToolMensajes",
        typeof(RegistrarColaboradoresTool).Assembly);

    internal static class Mensajes
    {
        public static string ResultadoColaboradoresRegistrados =>
            ResourceManager.GetString(nameof(ResultadoColaboradoresRegistrados))!;

        public static string ColaboradoresObligatorio =>
            ResourceManager.GetString(nameof(ColaboradoresObligatorio))!;

        public static string ColaboradoresJsonInvalido =>
            ResourceManager.GetString(nameof(ColaboradoresJsonInvalido))!;

        public static string ColaboradoresListaVacia =>
            ResourceManager.GetString(nameof(ColaboradoresListaVacia))!;

        /// <summary>{0}: nombre del campo obligatorio en blanco.</summary>
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        /// <summary>{0}: valor recibido de fecha_inicio.</summary>
        public static string FechaInvalida =>
            ResourceManager.GetString(nameof(FechaInvalida))!;

        /// <summary>{0}: cuerpo de la respuesta del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
