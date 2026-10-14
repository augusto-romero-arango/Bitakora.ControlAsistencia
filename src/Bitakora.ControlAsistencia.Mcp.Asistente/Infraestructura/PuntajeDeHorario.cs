namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public enum TipoCoincidenciaDeHorario
{
    Exacto,
    Cercano,
    SeCruza
}

public sealed record CoincidenciaDeHorario(
    TipoCoincidenciaDeHorario Tipo,
    int DistanciaEnMinutos,
    FranjaFicha Franja);

public sealed class PuntajeDeHorario
{
    private const int MinutosPorDia = 1440;
    private const int UmbralCercanoEnMinutos = 120;

    private readonly int _inicio;
    private readonly int _fin;
    private readonly bool _esPunto;

    private PuntajeDeHorario(int inicio, int fin, bool esPunto)
    {
        _inicio = inicio;
        _fin = fin;
        _esPunto = esPunto;
    }

    public static bool TryParsear(string? valor, out PuntajeDeHorario puntaje)
    {
        puntaje = null!;
        var partes = (valor ?? string.Empty).Trim().Split('-');

        if (partes.Length == 1 && NotacionFranja.TryParseHora(partes[0].Trim(), out var hora))
        {
            var minutos = Minutos(hora);
            puntaje = new PuntajeDeHorario(minutos, minutos, esPunto: true);
            return true;
        }

        if (partes.Length == 2
            && NotacionFranja.TryParseHora(partes[0].Trim(), out var inicio)
            && NotacionFranja.TryParseHora(partes[1].Trim(), out var fin))
        {
            var minutosInicio = Minutos(inicio);
            var minutosFin = Minutos(fin);
            if (minutosFin <= minutosInicio)
                minutosFin += MinutosPorDia;

            puntaje = new PuntajeDeHorario(minutosInicio, minutosFin, esPunto: false);
            return true;
        }

        return false;
    }

    /// <summary>Mejor coincidencia entre las franjas, o null si el turno no entra.</summary>
    public CoincidenciaDeHorario? Evaluar(IReadOnlyList<FranjaFicha> franjas) =>
        franjas
            .Select(EvaluarFranja)
            .OfType<CoincidenciaDeHorario>()
            .OrderBy(c => c.DistanciaEnMinutos)
            .FirstOrDefault();

    private CoincidenciaDeHorario? EvaluarFranja(FranjaFicha franja)
    {
        var inicioFranja = Minutos(franja.HoraInicio);
        var finFranja = Minutos(franja.HoraFin) + franja.DiaOffsetFin * MinutosPorDia;

        var distancias = new[] { 0, MinutosPorDia }
            .Select(desplazamiento => (Inicio: _inicio + desplazamiento, Fin: _fin + desplazamiento))
            .Where(r => Entra(r.Inicio, r.Fin, inicioFranja, finFranja))
            .Select(r => Math.Abs(r.Inicio - inicioFranja) + (_esPunto ? 0 : Math.Abs(r.Fin - finFranja)))
            .ToList();

        if (distancias.Count == 0)
            return null;

        var distancia = distancias.Min();
        var tipo = distancia == 0 ? TipoCoincidenciaDeHorario.Exacto
            : distancia <= UmbralCercanoEnMinutos ? TipoCoincidenciaDeHorario.Cercano
            : TipoCoincidenciaDeHorario.SeCruza;

        return new CoincidenciaDeHorario(tipo, distancia, franja);
    }

    private bool Entra(int inicio, int fin, int inicioFranja, int finFranja) =>
        _esPunto
            ? inicioFranja <= inicio && inicio < finFranja
            : inicio < finFranja && inicioFranja < fin;

    private static int Minutos(TimeOnly hora) => hora.Hour * 60 + hora.Minute;
}
