namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

// Segmento continuo de trabajo dentro de un turno.
// Puede contener FranjaDescanso y FranjaExtra.
// CA-4: tiene DiaOffsetFin (0 = mismo dia, 1 = dia siguiente)
// ADR-0015: record con factory estatico, constructor privado.
public sealed record FranjaOrdinaria : FranjaTemporal
{
    // Constructor vacio privado para compatibilidad con Marten/JSON (CA-9)
    private FranjaOrdinaria() { }

    // CA-4: offset del fin respecto al dia de inicio
    public int DiaOffsetFin { get; private init; }

    public IReadOnlyList<FranjaDescanso> Descansos { get; private init; } = [];
    public IReadOnlyList<FranjaExtra> Extras { get; private init; } = [];

    internal override int MinutosAbsolutoInicio => CalcularMinutosAbsolutos(HoraInicio, 0);
    internal override int MinutosAbsolutoFin => CalcularMinutosAbsolutos(HoraFin, DiaOffsetFin);

    // CA-8: factory estatico
    // CA-7: rechaza InicioYFinIguales
    // CA-13: infiere offset +1 cuando fin < inicio
    // CA-14 a CA-16: valida que descansos y extras esten contenidos
    // CA-17 a CA-19: valida que descansos y extras no se solapen entre si
    public static FranjaOrdinaria Crear(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetFin = 0,
        IEnumerable<FranjaDescanso>? descansos = null,
        IEnumerable<FranjaExtra>? extras = null)
    {
        // CA-13: inferir offset cuando fin < inicio y no se especifico
        if (diaOffsetFin == 0 && horaFin < horaInicio)
            diaOffsetFin = 1;

        // CA-7: rechazar duracion cero
        if (horaInicio == horaFin && diaOffsetFin == 0)
            throw new ArgumentException(Mensajes.InicioYFinIguales);

        var listaDescansos = descansos?.ToList() ?? [];
        var listaExtras = extras?.ToList() ?? [];

        var ordinaria = new FranjaOrdinaria
        {
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            DiaOffsetFin = diaOffsetFin,
            Descansos = listaDescansos,
            Extras = listaExtras
        };

        // CA-14 a CA-16: validar contencion de todas las hijas
        ValidarContencion(ordinaria, listaDescansos, listaExtras);

        // CA-17 a CA-19: validar que no haya solapamiento entre hijas
        ValidarSolapamiento(listaDescansos, listaExtras);

        return ordinaria;
    }

    // CA-20, CA-21: formato "(06:00-12:00)" o "(22:00-06:00+1)"
    public override string ToString()
    {
        var resultado = $"({FormatearHora(HoraInicio, 0)}-{FormatearHora(HoraFin, DiaOffsetFin)})";

        if (Descansos.Count > 0)
            resultado += $"[{Mensajes.LabelDescansos}:{string.Join(", ", Descansos)}]";

        if (Extras.Count > 0)
            resultado += $"[{Mensajes.LabelExtras}:{string.Join(", ", Extras)}]";

        return resultado;
    }

    // Verifica que cada franja hija este contenida dentro de la ordinaria
    private static void ValidarContencion(
        FranjaOrdinaria contenedor,
        IReadOnlyList<FranjaDescanso> descansos,
        IReadOnlyList<FranjaExtra> extras)
    {
        var inicioContenedor = contenedor.MinutosAbsolutoInicio;
        var finContenedor = contenedor.MinutosAbsolutoFin;

        foreach (var descanso in descansos)
        {
            if (!EstaContenida(inicioContenedor, finContenedor,
                    descanso.MinutosAbsolutoInicio, descanso.MinutosAbsolutoFin))
                throw new ArgumentException(Mensajes.FranjaHijaFueraDeContenedor);
        }

        foreach (var extra in extras)
        {
            if (!EstaContenida(inicioContenedor, finContenedor,
                    extra.MinutosAbsolutoInicio, extra.MinutosAbsolutoFin))
                throw new ArgumentException(Mensajes.FranjaHijaFueraDeContenedor);
        }
    }

    // Contencion: hijo debe empezar >= padre inicio y terminar <= padre fin
    private static bool EstaContenida(int inicioContenedor, int finContenedor,
        int inicioHija, int finHija) =>
        inicioHija >= inicioContenedor && finHija <= finContenedor;

    // Verifica que ninguna par de franjas hijas se solapen
    private static void ValidarSolapamiento(
        IReadOnlyList<FranjaDescanso> descansos,
        IReadOnlyList<FranjaExtra> extras)
    {
        // Convertir todas las hijas a tuplas (inicio, fin) en minutos absolutos
        var rangos = descansos
            .Select(d => (Inicio: d.MinutosAbsolutoInicio, Fin: d.MinutosAbsolutoFin))
            .Concat(extras.Select(e => (Inicio: e.MinutosAbsolutoInicio, Fin: e.MinutosAbsolutoFin)))
            .ToList();

        // Verificar cada par - CA-18: fin exclusivo, contiguas no se solapan
        for (var i = 0; i < rangos.Count; i++)
        {
            for (var j = i + 1; j < rangos.Count; j++)
            {
                if (SeSolapan(rangos[i], rangos[j]))
                    throw new ArgumentException(Mensajes.FranjasHijasSeSuperponen);
            }
        }
    }

    // Dos rangos se solapan si uno empieza antes de que el otro termine (fin exclusivo)
    private static bool SeSolapan((int Inicio, int Fin) a, (int Inicio, int Fin) b) =>
        a.Inicio < b.Fin && b.Inicio < a.Fin;
}
