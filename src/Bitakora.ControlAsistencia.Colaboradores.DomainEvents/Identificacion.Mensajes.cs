using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

public sealed partial class Identificacion
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.DomainEvents.IdentificacionMensajes",
        typeof(Identificacion).Assembly);

    public static class Mensajes
    {
        // "Vacio" significa vacio DESPUES de la limpieza del issue #381, no solo un string vacio:
        // "---" o ".." tambien lo disparan porque no dejan ningun caracter alfanumerico.
        public static string NumeroVacio =>
            ResourceManager.GetString(nameof(NumeroVacio))!;
    }
}
