namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

// MEF-ADR-0012: la interseccion pertenece al VO; no exponer Desde/Hasta para un helper externo.
public sealed partial class VentanaDeProgramacion
{
    internal const int MaximoDias = 35;

    private readonly DateOnly _desde;
    private readonly DateOnly _hasta;

    private VentanaDeProgramacion(DateOnly desde, DateOnly hasta)
    {
        _desde = desde;
        _hasta = hasta;
    }

    public static VentanaDeProgramacion Crear(DateOnly desde, DateOnly hasta)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Interseccion dia a dia de la ventana con [vigenteDesde, vigenteHasta] (vigenteHasta null =
    /// vigencia abierta). Vacia si no se tocan.
    /// </summary>
    public IReadOnlyList<DateOnly> DiasCubiertosPor(DateOnly vigenteDesde, DateOnly? vigenteHasta)
    {
        throw new NotImplementedException();
    }

    public override string ToString() => throw new NotImplementedException();
}
