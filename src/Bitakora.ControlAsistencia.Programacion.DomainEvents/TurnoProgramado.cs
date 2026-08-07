namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Representacion plana del turno programado, propia del dominio Programacion.
/// </summary>
/// <remarks>
/// Issue #319 (tres islas, MEF-ADR-0039 decision 2 y 6): payload propio de este ensamblado con
/// paridad exacta de campos con DetalleTurno (PrivateEvents), incluyendo que FranjasOrdinarias
/// tipa con FranjaProgramada, no con DetalleFranjaOrdinaria. CatalogoTurnos.ObtenerDetalle()
/// retorna este tipo -- lo usa ProgramacionTurnoSolicitada (el evento que SE PERSISTE); el FA
/// mapea a DetalleTurno solo para los eventos que cruzan el bus (CA-5).
///
/// El calificador "Programado" es de dominio ("el turno programado", ver ControlFranja en el
/// glosario), no de infraestructura -- distinto de "Detalle*" (el DTO de bus). Equals/GetHashCode
/// propios comparan FranjasOrdinarias POR VALOR (SequenceEqual) y EXCLUYEN Descripcion (dato
/// derivado, no identidad del turno) -- mismo criterio que DetalleTurno (issue #288).
/// </remarks>
public record TurnoProgramado(
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

    public virtual bool Equals(TurnoProgramado? other)
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

    /// <summary>
    /// Issue #341: cascada de sede al copiar el turno del catalogo en la solicitud de
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
    public TurnoProgramado ConSedePorDefecto(SedeProgramada? sedePorDefecto) =>
        throw new NotImplementedException();
}
