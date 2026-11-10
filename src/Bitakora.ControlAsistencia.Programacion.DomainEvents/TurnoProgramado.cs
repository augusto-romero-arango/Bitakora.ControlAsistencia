namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Mismos nombres de campo que DetalleTurno (PrivateEvents) y TurnoDiario (ControlHoras.DomainEvents),
/// pero FranjasOrdinarias tipa con FranjaProgramada; el FA lo mapea campo a campo a DetalleTurno para
/// el bus. Equals/GetHashCode comparan FranjasOrdinarias por valor, incluyen la amplitud y
/// EsHorarioAbierto y excluyen Descripcion (derivado).
/// </summary>
public record TurnoProgramado(
    string Nombre,
    IReadOnlyList<FranjaProgramada> FranjasOrdinarias,
    string Descripcion,
    int AmplitudVentanaEntradaEnMinutos = 0,
    bool EsHorarioAbierto = false)
{
    /// <summary>Los eventos persistidos antes de existir el campo lo traen null: se normaliza a cadena vacia.</summary>
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
    /// Cascada de sede: cada franja toma <c>franja.Sede ?? sedePorDefecto</c>, asi que la sede del
    /// catalogo le gana al default de la solicitud. No muta esta instancia. Con
    /// <paramref name="sedePorDefecto"/> null es identidad: el handler la invoca sin ramificar.
    /// </summary>
    public TurnoProgramado ConSedePorDefecto(SedeProgramada? sedePorDefecto) => this with
    {
        FranjasOrdinarias = FranjasOrdinarias
            .Select(franja => franja with { Sede = franja.Sede ?? sedePorDefecto })
            .ToList()
            .AsReadOnly()
    };

    /// <summary>
    /// Bloques del turno en <paramref name="fecha"/>: cada franja recortada por sus descansos y extras,
    /// con su sede.
    /// </summary>
    public IReadOnlyList<BloqueProgramado> Segmentar(DateOnly fecha) =>
        FranjasOrdinarias.SelectMany(franja => franja.Segmentar(fecha)).ToList();
}
