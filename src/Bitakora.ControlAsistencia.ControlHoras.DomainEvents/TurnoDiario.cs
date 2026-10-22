namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Representacion plana del turno que rige efectivamente a un colaborador en una fecha concreta,
/// propia de este ensamblado (payload por rol -- CA-ADR-0029 decision #5 / MEF-ADR-0039
/// decision #6). Duplica los campos de DetalleTurno (PrivateEvents.Programacion) con los mismos
/// nombres en vez de importarlo: los tres ensamblados de eventos son tres islas sin referencias
/// entre si, y ningun test fija esta paridad. "TurnoDiario" es el termino exacto del glosario
/// (docs/eda/ubiquitous-language.yaml).
/// </summary>
/// <remarks>
/// Equals/GetHashCode propios comparan FranjasOrdinarias POR VALOR (SequenceEqual) y EXCLUYEN
/// Descripcion (dato derivado, no identidad del turno) -- mismo criterio que DetalleTurno (issue #288)
/// y TurnoProgramado (Programacion.DomainEvents, issue #319). La amplitud de la ventana de entrada SI
/// entra: es dato de identidad (los streams legados sin la clave leen 0).
/// </remarks>
public record TurnoDiario(
    string Nombre,
    IReadOnlyList<FranjaProgramada> FranjasOrdinarias,
    string Descripcion,
    int AmplitudVentanaEntradaEnMinutos = 0)
{
    /// <summary>
    /// Los eventos anteriores a este record no llevan el campo: STJ deja null en el parametro
    /// posicional y la propiedad declarada como string no anulable mentiria. Se normaliza a
    /// cadena vacia -- mismo criterio que DetalleTurno (issue #288).
    /// </summary>
    public string Descripcion { get; init; } = Descripcion ?? string.Empty;

    private const int UltimoMinutoDelDia = 1439;

    public bool TieneVentanaDeEntrada => AmplitudVentanaEntradaEnMinutos > 0;

    /// <summary>
    /// Desplaza todas las franjas para que la apertura coincida con la primera marcacion, acotada a la
    /// ventana de entrada. El cierre se acota tambien para que el inicio de la ultima franja no pase al
    /// dia siguiente (FranjaProgramada fija el inicio en offset 0): el calculo es total aunque el turno
    /// viole esa invariante del diseno (MEF-ADR-0004, Apply sin excepciones).
    /// </summary>
    public TurnoDiario HorarioEfectivo(DateOnly fecha, DateTime? primeraMarcacion)
    {
        if (!TieneVentanaDeEntrada || primeraMarcacion is null || FranjasOrdinarias.Count == 0)
            return this;

        var apertura = FranjasOrdinarias.Min(f => Tramo.MinutosAbsolutos(f.HoraInicio, 0));
        var inicioUltimaFranja = FranjasOrdinarias.Max(f => Tramo.MinutosAbsolutos(f.HoraInicio, 0));
        var desplazamientoMaximo = Math.Max(0, Math.Min(
            AmplitudVentanaEntradaEnMinutos, UltimoMinutoDelDia - inicioUltimaFranja));

        var ancla = (int)Math.Floor((primeraMarcacion.Value - fecha.ToDateTime(TimeOnly.MinValue)).TotalMinutes);
        var desplazamiento = Math.Clamp(ancla - apertura, 0, desplazamientoMaximo);
        if (desplazamiento == 0)
            return this;

        return this with
        {
            FranjasOrdinarias = FranjasOrdinarias.Select(f => f.Desplazar(desplazamiento)).ToList()
        };
    }

    private const int MargenAntesDeApertura = 120;
    private const int MargenDespuesDeFin = 240;

    public CoberturaDeMarcacion? Cobertura(DateOnly fecha)
    {
        if (FranjasOrdinarias.Count == 0)
            return null;

        var apertura = FranjasOrdinarias.Min(f => fecha.ToDateTime(f.HoraInicio));
        var fin = FranjasOrdinarias.Max(f => fecha.AddDays(f.DiaOffsetFin).ToDateTime(f.HoraFin));
        return new CoberturaDeMarcacion(
            apertura.AddMinutes(-MargenAntesDeApertura),
            fin.AddMinutes(MargenDespuesDeFin));
    }

    public virtual bool Equals(TurnoDiario? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Nombre == other.Nombre
            && AmplitudVentanaEntradaEnMinutos == other.AmplitudVentanaEntradaEnMinutos
            && FranjasOrdinarias.SequenceEqual(other.FranjasOrdinarias);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Nombre);
        hash.Add(AmplitudVentanaEntradaEnMinutos);
        foreach (var franja in FranjasOrdinarias) hash.Add(franja);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Segmenta el turno en bloques absolutos de tiempo (issue #327): cada franja se recorta por sus
    /// descansos y extras, cada tramo resultante se rompe en la medianoche que cruce y se resuelve
    /// contra <paramref name="fecha"/> (el dia de asignacion del turno). Lectura pura -- no muta el
    /// turno ni persiste el resultado (MEF-ADR-0012, Tell-don't-Ask: cada paso lo ejecuta el objeto
    /// dueno del dato, no un calculo externo sobre TimeOnly/DiaOffset crudos).
    /// </summary>
    public IReadOnlyList<BloqueTurno> Segmentar(DateOnly fecha) =>
        FranjasOrdinarias
            .SelectMany(franja => franja.Segmentar())
            .SelectMany(tramo => tramo.RomperEnMedianoche())
            .Select(tramo => tramo.ResolverA(fecha))
            .ToList();

    /// <summary>
    /// Suma de los tramos ordinarios de todas sus franjas (0 si no tiene franjas). Un turno que cruza
    /// la medianoche cuenta completo: pertenece al dia en que empieza.
    /// </summary>
    public int MinutosOrdinarios() =>
        FranjasOrdinarias
            .SelectMany(franja => franja.Segmentar())
            .Where(tramo => tramo.Tipo == TipoBloque.Ordinaria)
            .Sum(tramo => tramo.Fin - tramo.Inicio);
}
