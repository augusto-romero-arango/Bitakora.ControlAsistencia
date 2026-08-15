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

        // Issue #376: Parsear() lo lanza cuando el valor no trae NINGUN guion -- el tipo/numero no
        // se pueden separar. Los otros dos rechazos de Parsear (tipo fuera de la lista, numero vacio
        // tras limpiar) reusan los mensajes propios de TipoIdentificacion.Desde/Crear.
        public static string FormatoInvalido =>
            ResourceManager.GetString(nameof(FormatoInvalido))!;
    }
}
