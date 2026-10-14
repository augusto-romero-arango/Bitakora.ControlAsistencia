using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarMallaTurnos;

public partial class ConsultarMallaTurnosTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarMallaTurnos.ConsultarMallaTurnosToolMensajes",
        typeof(ConsultarMallaTurnosTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: valor recibido.</summary>
        public static string FechaInvalida => ResourceManager.GetString(nameof(FechaInvalida))!;

        /// <summary>{0}: cuerpo de la respuesta 400/422 del dominio.</summary>
        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;

        /// <summary>{0}: criterio del grupo, {1}: semana.</summary>
        public static string GrupoSinColaboradores => ResourceManager.GetString(nameof(GrupoSinColaboradores))!;

        /// <summary>{0}: cursor a reenviar.</summary>
        public static string NotaMasColaboradores => ResourceManager.GetString(nameof(NotaMasColaboradores))!;
    }
}
