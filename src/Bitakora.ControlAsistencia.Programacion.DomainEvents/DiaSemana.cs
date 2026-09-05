namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #621: dia de la semana de una plantilla semanal (CA-ADR-0034), lista cerrada de 7
// instancias canonicas -- mismo patron que TipoIdentificacion (MEF-ADR-0012): ctor privado, NUNCA
// un enum C#. El numero es ISO 8601 (1 = lunes .. 7 = domingo), deliberadamente distinto de
// System.DayOfWeek (domingo = 0): persistir el numero ISO evita heredar esa sombra de la
// plataforma en el store y en cada consumidor. Desde(DayOfWeek) y una etiqueta .resx localizada
// llegan con su primer consumidor real (asignacion futura) -- "sin artefactos huerfanos".
public sealed partial class DiaSemana
{
    public static readonly DiaSemana Lunes = new(1);
    public static readonly DiaSemana Martes = new(2);
    public static readonly DiaSemana Miercoles = new(3);
    public static readonly DiaSemana Jueves = new(4);
    public static readonly DiaSemana Viernes = new(5);
    public static readonly DiaSemana Sabado = new(6);
    public static readonly DiaSemana Domingo = new(7);

    // Lookup map por clave discreta (numero -> instancia), no switch/if: agregar un dia nuevo seria
    // agregar una fila (mismo patron TipoIdentificacion.PorCodigo).
    private static readonly IReadOnlyDictionary<int, DiaSemana> PorNumero = new Dictionary<int, DiaSemana>
    {
        [Lunes._numero] = Lunes,
        [Martes._numero] = Martes,
        [Miercoles._numero] = Miercoles,
        [Jueves._numero] = Jueves,
        [Viernes._numero] = Viernes,
        [Sabado._numero] = Sabado,
        [Domingo._numero] = Domingo,
    };

    private readonly int _numero;

    private DiaSemana(int numero) => _numero = numero;

    public int Numero => _numero;

    // CA-1: rehidrata desde el numero ISO persistido; rechaza numeros fuera de 1..7.
    public static DiaSemana Desde(int numero) =>
        PorNumero.TryGetValue(numero, out var dia)
            ? dia
            : throw new ArgumentException(Mensajes.NumeroFueraDeRango, nameof(numero));
}
