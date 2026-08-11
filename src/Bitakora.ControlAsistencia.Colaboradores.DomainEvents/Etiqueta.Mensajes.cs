using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

public sealed partial class Etiqueta
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.DomainEvents.EtiquetaMensajes",
        typeof(Etiqueta).Assembly);

    public static class Mensajes
    {
        public static string CategoriaVacia =>
            ResourceManager.GetString(nameof(CategoriaVacia))!;

        public static string ValorVacio =>
            ResourceManager.GetString(nameof(ValorVacio))!;
    }
}
