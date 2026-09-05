namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Lista cerrada de 7 instancias canonicas con ctor privado -- NUNCA un enum C# (MEF-ADR-0012,
// patron TipoIdentificacion). El numero es ISO 8601 (1 = lunes .. 7 = domingo), deliberadamente
// distinto de System.DayOfWeek (domingo = 0): es el numero que se persiste, asi que renumerarlo
// para alinearlo con la plataforma invalidaria todo lo ya escrito en el store.
public sealed partial class DiaSemana
{
    public static readonly DiaSemana Lunes = new(1);
    public static readonly DiaSemana Martes = new(2);
    public static readonly DiaSemana Miercoles = new(3);
    public static readonly DiaSemana Jueves = new(4);
    public static readonly DiaSemana Viernes = new(5);
    public static readonly DiaSemana Sabado = new(6);
    public static readonly DiaSemana Domingo = new(7);

    private static readonly IReadOnlyDictionary<int, DiaSemana> PorNumero = new Dictionary<int, DiaSemana>
    {
        [Lunes.Numero] = Lunes,
        [Martes.Numero] = Martes,
        [Miercoles.Numero] = Miercoles,
        [Jueves.Numero] = Jueves,
        [Viernes.Numero] = Viernes,
        [Sabado.Numero] = Sabado,
        [Domingo.Numero] = Domingo,
    };

    private DiaSemana(int numero) => Numero = numero;

    public int Numero { get; }

    // Igualdad por referencia: Desde() siempre retorna la instancia canonica, nunca crea una nueva.
    // De eso depende la clave (int, DiaSemana) del diccionario de dias de PlantillaSemanalTurnos.
    public static DiaSemana Desde(int numero) =>
        PorNumero.TryGetValue(numero, out var dia)
            ? dia
            : throw new ArgumentException(Mensajes.NumeroFueraDeRango, nameof(numero));
}
