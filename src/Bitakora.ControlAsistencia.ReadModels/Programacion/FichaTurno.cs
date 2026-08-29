namespace Bitakora.ControlAsistencia.ReadModels.Programacion;

/// <summary>
/// Ficha de un turno del catalogo (issue #496): permite al Programador (front hoy, asistente MCP
/// en la vision de largo plazo) resolver Nombre -> TurnoId y confirmar si el turno que necesita ya
/// existe antes de crear uno nuevo. Sigue la familia acunada FichaColaborador/FichaSede -- la ficha
/// de un elemento de catalogo (MEF-ADR-0041).
/// </summary>
/// <remarks>
/// Record plano SIN partial (MEF-ADR-0035, skills/projections/modelos-marten.md): el comportamiento
/// de proyeccion vive en la clase companion FichaTurnoProjection, en el worker
/// (Bitakora.ControlAsistencia.Projections). Este tipo vive en ReadModels, la cuarta isla del repo
/// -- cero referencias de proyecto (ver el .csproj): FranjaFicha/SubFranjaFicha son propios, sin
/// relacion de tipo con Programacion.DomainEvents (MEF-ADR-0039 decision 2, enmendada por
/// MEF-ADR-0041 decision 1).
///
/// Sin sufijo "View" (MEF-ADR-0041 decision 3).
///
/// Id es el stream key del catalogo -- evento.TurnoId.ToString() (mismo criterio que
/// CatalogoTurnos.Apply, Events.StreamIdentity = AsString) -- nunca un Guid crudo.
///
/// EsDescanso se DERIVA de la variante del evento (TurnoCreado.CrearDescanso, franjas vacias): no
/// existe como campo propio del aggregate -- coherente con #423, la marca vive en la frontera de
/// lectura, no en el escritor.
///
/// HorarioResumido es la confirmacion RAPIDA ("el de 06:00-14:00?", espejo del patron de
/// TurnoVigente/#328) -- se compone en la projection, nunca aqui. Descripcion es la version
/// COMPLETA para desambiguar entre turnos de nombre parecido (incluye descansos/extras/sede de cada
/// franja); tambien se compone en la projection, nunca aqui (MEF-ADR-0041 decision 2).
/// </remarks>
public sealed record FichaTurno(
    string Id,
    string Nombre,
    bool EsDescanso,
    string HorarioResumido,
    IReadOnlyList<FranjaFicha> Franjas,
    string Descripcion)
{
    // Igualdad por valor sobre Franjas (SequenceEqual): la igualdad de record por defecto compara
    // IReadOnlyList<T> por EqualityComparer<T>.Default -- referencia, no valor -- para List<T>/
    // arrays (ADR-0015, mismo gotcha que motivo el Equals a mano de FranjaProgramada en
    // Programacion.DomainEvents). Sin este override, dos FichaTurno con las mismas franjas en
    // instancias de lista distintas no serian iguales para FichaTurnoProjectionTests.
    public bool Equals(FichaTurno? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id
            && Nombre == other.Nombre
            && EsDescanso == other.EsDescanso
            && HorarioResumido == other.HorarioResumido
            && Descripcion == other.Descripcion
            && Franjas.SequenceEqual(other.Franjas);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Nombre);
        hash.Add(EsDescanso);
        hash.Add(HorarioResumido);
        hash.Add(Descripcion);
        foreach (var franja in Franjas) hash.Add(franja);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Franja completa de un turno del catalogo -- forma propia de ReadModels, espejo deliberado de
/// FranjaOrdinaria (Programacion.DomainEvents) sin importar ese tipo (MEF-ADR-0041 decision 1): el
/// consumidor debe poder responder "hay descansos o extras dentro de esta franja?" y "trae sede
/// prearmada?" sin ir al event store.
/// </summary>
/// <remarks>
/// Nombre "propuesta revisable" (issue #496, "Vista a materializar"): espejo de EtiquetaFicha en la
/// familia de tipos anidados de una ficha de catalogo.
/// </remarks>
public sealed record FranjaFicha(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetFin,
    IReadOnlyList<SubFranjaFicha> Descansos,
    IReadOnlyList<SubFranjaFicha> Extras,
    string? SedeId,
    string? NombreSede,
    string Descripcion)
{
    // Mismo gotcha de FichaTurno.Equals: Descansos/Extras necesitan SequenceEqual, no la igualdad
    // de record por defecto (referencia sobre List<T>/arrays, ADR-0015).
    public bool Equals(FranjaFicha? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return HoraInicio == other.HoraInicio
            && HoraFin == other.HoraFin
            && DiaOffsetFin == other.DiaOffsetFin
            && SedeId == other.SedeId
            && NombreSede == other.NombreSede
            && Descripcion == other.Descripcion
            && Descansos.SequenceEqual(other.Descansos)
            && Extras.SequenceEqual(other.Extras);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(HoraInicio);
        hash.Add(HoraFin);
        hash.Add(DiaOffsetFin);
        hash.Add(SedeId);
        hash.Add(NombreSede);
        hash.Add(Descripcion);
        foreach (var descanso in Descansos) hash.Add(descanso);
        foreach (var extra in Extras) hash.Add(extra);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Sub-franja (descanso o extra) contenida en una <see cref="FranjaFicha"/> -- forma propia de
/// ReadModels, espejo deliberado de SubFranjaProgramada (Programacion.DomainEvents) sin importar
/// ese tipo (MEF-ADR-0041 decision 1).
/// </summary>
public sealed record SubFranjaFicha(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin);
