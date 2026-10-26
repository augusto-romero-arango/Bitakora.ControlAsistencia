namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Representacion plana del turno que viaja en eventos entre dominios.
/// No tiene comportamiento de dominio, solo datos.
/// </summary>
/// <remarks>
/// Mismos nombres de campo que TurnoProgramado (Programacion.DomainEvents) y TurnoDiario
/// (ControlHoras.DomainEvents): los Function Apps mapean campo a campo entre islas. Plano y
/// portable (CA-ADR-0025): la amplitud viaja como int en minutos, nunca como HorasYMinutos.
///
/// Descripcion es la representacion textual normalizada del turno (CatalogoTurnos.ToString()),
/// dato derivado. El record por defecto compararia FranjasOrdinarias (IReadOnlyList) por
/// referencia: Equals/GetHashCode propios comparan Nombre, AmplitudVentanaEntradaEnMinutos,
/// EsHorarioAbierto y FranjasOrdinarias POR VALOR (SequenceEqual) y EXCLUYEN Descripcion.
/// </remarks>
public record DetalleTurno(
    string Nombre,
    IReadOnlyList<DetalleFranjaOrdinaria> FranjasOrdinarias,
    string Descripcion,
    int AmplitudVentanaEntradaEnMinutos = 0,
    bool EsHorarioAbierto = false)
{
    /// <summary>
    /// Los eventos anteriores al issue #288 no llevan el campo: STJ deja null en el parametro
    /// posicional y la propiedad declarada como string no anulable mentiria. Se normaliza a
    /// cadena vacia, que es la consecuencia que el issue asumio para los eventos ya persistidos.
    /// </summary>
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
