using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class FranjaOrdinaria
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.FranjaOrdinariaMensajes",
        typeof(FranjaOrdinaria).Assembly);

    // 'new': oculta FranjaTemporal.Mensajes (heredado, compartido con SubFranja) sin reemplazarlo; aqui
    // solo lo exclusivo de FranjaOrdinaria (sede y tope de 24 horas).
    internal new static class Mensajes
    {
        public static string SedeIncompleta =>
            ResourceManager.GetString(nameof(SedeIncompleta))!;

        public static string DuracionExcedeUnDia =>
            ResourceManager.GetString(nameof(DuracionExcedeUnDia))!;
    }
}
