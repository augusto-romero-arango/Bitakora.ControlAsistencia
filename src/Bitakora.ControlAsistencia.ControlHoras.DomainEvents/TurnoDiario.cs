namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Representacion plana del turno que rige efectivamente a un empleado en una fecha concreta,
/// propia de este ensamblado (issue #322, payload por rol -- CA-ADR-0029 decision #5 /
/// MEF-ADR-0039 decision #6). Duplica con paridad exacta de campos a DetalleTurno
/// (PrivateEvents.Programacion) en vez de importarlo: los tres ensamblados de eventos son tres
/// islas sin referencias entre si. Nombre acordado con el usuario (docs/eda/ubiquitous-language.yaml):
/// "TurnoDiario" es el termino exacto del glosario para este concepto.
/// </summary>
/// <remarks>
/// Equals/GetHashCode propios comparan FranjasOrdinarias POR VALOR (SequenceEqual) y EXCLUYEN
/// Descripcion (dato derivado, no identidad del turno) -- mismo criterio que DetalleTurno
/// (issue #288) y TurnoProgramado (Programacion.DomainEvents, issue #319).
/// </remarks>
public record TurnoDiario(
    string Nombre,
    IReadOnlyList<FranjaProgramada> FranjasOrdinarias,
    string Descripcion)
{
    /// <summary>
    /// Los eventos anteriores a este record no llevan el campo: STJ deja null en el parametro
    /// posicional y la propiedad declarada como string no anulable mentiria. Se normaliza a
    /// cadena vacia -- mismo criterio que DetalleTurno (issue #288).
    /// </summary>
    public string Descripcion { get; init; } = Descripcion ?? string.Empty;

    public virtual bool Equals(TurnoDiario? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Nombre == other.Nombre
            && FranjasOrdinarias.SequenceEqual(other.FranjasOrdinarias);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Nombre);
        foreach (var franja in FranjasOrdinarias) hash.Add(franja);
        return hash.ToHashCode();
    }
}
