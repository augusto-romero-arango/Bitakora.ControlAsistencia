namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Representacion plana del turno programado, propia del dominio Programacion.
/// </summary>
/// <remarks>
/// Payload por rol (tres islas, MEF-ADR-0039 decisiones 2 y 6): mismos nombres de campo que
/// DetalleTurno (PrivateEvents) y TurnoDiario (ControlHoras.DomainEvents), pero FranjasOrdinarias
/// tipa con FranjaProgramada, no con DetalleFranjaOrdinaria. Lo persiste
/// ProgramacionTurnoSolicitada; el FA lo mapea campo a campo a DetalleTurno para el bus.
///
/// Equals/GetHashCode comparan FranjasOrdinarias POR VALOR (SequenceEqual), incluyen
/// AmplitudVentanaEntradaEnMinutos y EsHorarioAbierto (identidad del turno) y EXCLUYEN Descripcion
/// (dato derivado) --
/// mismo criterio que DetalleTurno y TurnoDiario.
/// </remarks>
public record TurnoProgramado(
    string Nombre,
    IReadOnlyList<FranjaProgramada> FranjasOrdinarias,
    string Descripcion,
    int AmplitudVentanaEntradaEnMinutos = 0,
    bool EsHorarioAbierto = false)
{
    /// <summary>
    /// Los eventos anteriores a este record no llevan el campo: STJ deja null en el parametro
    /// posicional y la propiedad declarada como string no anulable mentiria. Se normaliza a
    /// cadena vacia -- mismo criterio que DetalleTurno (issue #288).
    /// </summary>
    public string Descripcion { get; init; } = Descripcion ?? string.Empty;

    public virtual bool Equals(TurnoProgramado? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Nombre == other.Nombre
            && AmplitudVentanaEntradaEnMinutos == other.AmplitudVentanaEntradaEnMinutos
            && EsHorarioAbierto == other.EsHorarioAbierto
            && FranjasOrdinarias.SequenceEqual(other.FranjasOrdinarias);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Nombre);
        hash.Add(AmplitudVentanaEntradaEnMinutos);
        hash.Add(EsHorarioAbierto);
        foreach (var franja in FranjasOrdinarias) hash.Add(franja);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Cascada de sede al copiar el turno del catalogo en la solicitud de
    /// programacion. Cada franja resuelve su sede efectiva con
    /// <c>franja.Sede ?? sedePorDefecto</c> -- el catalogo (intencion explicita de diseno) le gana
    /// al default de la solicitud. Funcion pura sobre records (<c>with</c>): no muta esta
    /// instancia, retorna un turno nuevo. Con <paramref name="sedePorDefecto"/> null la cascada es
    /// identidad (permite invocacion incondicional desde el handler, sin ramificar por si la
    /// solicitud trae sede).
    /// </summary>
    /// <remarks>
    /// Tell-don't-Ask (MEF-ADR-0012): la cascada vive en el dueno de las franjas, no en el
    /// handler -- mismo precedente que TurnoDiario.Segmentar (#327).
    /// </remarks>
    public TurnoProgramado ConSedePorDefecto(SedeProgramada? sedePorDefecto) => this with
    {
        FranjasOrdinarias = FranjasOrdinarias
            .Select(franja => franja with { Sede = franja.Sede ?? sedePorDefecto })
            .ToList()
            .AsReadOnly()
    };
}
