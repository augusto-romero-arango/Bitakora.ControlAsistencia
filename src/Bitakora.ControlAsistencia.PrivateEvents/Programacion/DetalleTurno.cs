namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Mismos nombres de campo que TurnoProgramado (Programacion.DomainEvents) y TurnoDiario
/// (ControlHoras.DomainEvents): los Function Apps mapean campo a campo entre islas. La amplitud viaja
/// como int en minutos, nunca como HorasYMinutos. Equals/GetHashCode comparan FranjasOrdinarias por
/// valor y excluyen Descripcion (derivada de CatalogoTurnos.ToString()).
/// </summary>
public record DetalleTurno(
    string Nombre,
    IReadOnlyList<DetalleFranjaOrdinaria> FranjasOrdinarias,
    string Descripcion,
    int AmplitudVentanaEntradaEnMinutos = 0,
    bool EsHorarioAbierto = false)
{
    /// <summary>Los eventos persistidos antes de existir el campo lo traen null: se normaliza a cadena vacia.</summary>
    public string Descripcion { get; init; } = Descripcion ?? string.Empty;

    public virtual bool Equals(DetalleTurno? other)
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
}
