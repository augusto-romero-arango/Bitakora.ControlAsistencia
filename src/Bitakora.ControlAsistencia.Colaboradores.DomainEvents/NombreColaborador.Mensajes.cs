using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

public sealed partial class NombreColaborador
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.DomainEvents.NombreColaboradorMensajes",
        typeof(NombreColaborador).Assembly);

    public static class Mensajes
    {
        public static string PrimerNombreRequerido =>
            ResourceManager.GetString(nameof(PrimerNombreRequerido))!;

        public static string PrimerApellidoRequerido =>
            ResourceManager.GetString(nameof(PrimerApellidoRequerido))!;
    }
}
