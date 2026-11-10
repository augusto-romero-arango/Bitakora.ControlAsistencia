using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

public sealed partial class Identificacion
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.DomainEvents.IdentificacionMensajes",
        typeof(Identificacion).Assembly);

    public static class Mensajes
    {
        // Vacio DESPUES de la limpieza: "---" o ".." tambien lo disparan.
        public static string NumeroVacio =>
            ResourceManager.GetString(nameof(NumeroVacio))!;

        // Solo cuando el valor de Parsear() no trae ningun guion; sus otros rechazos reusan los
        // mensajes de TipoIdentificacion.Desde/Crear.
        public static string FormatoInvalido =>
            ResourceManager.GetString(nameof(FormatoInvalido))!;
    }
}
