namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Representacion plana del turno que viaja en eventos entre dominios.
/// No tiene comportamiento de dominio, solo datos.
/// </summary>
/// <remarks>
/// Issue #288: dos intervenciones sobre el record por defecto.
/// 1) Descripcion: representacion textual normalizada (formato del tipo rico
///    CatalogoTurnos.ToString()), dato derivado persistido -- ver decision del issue.
/// 2) Bug latente corregido: FranjasOrdinarias es IReadOnlyList y el record por defecto la compara
///    por referencia (ADR-0015; mismo bug que #129 ya corrigio en DetalleFranjaOrdinaria).
///    Equals/GetHashCode propios comparan Nombre y FranjasOrdinarias POR VALOR (SequenceEqual) y
///    EXCLUYEN Descripcion (dato derivado, no identidad del turno).
/// </remarks>
public record DetalleTurno(
    string Nombre,
    IReadOnlyList<DetalleFranjaOrdinaria> FranjasOrdinarias,
    string Descripcion)
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
