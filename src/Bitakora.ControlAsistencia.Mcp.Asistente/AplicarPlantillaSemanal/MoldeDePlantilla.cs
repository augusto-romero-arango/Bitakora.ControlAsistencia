using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanal;

internal sealed record MensajesDePlantilla(
    string PlantillaNoExiste, string PlantillaIncompleta, string PlantillaConTurnoNoProgramable,
    string RechazoDelDominio);

/// <summary>Plantilla semanal validada como aplicable; la comparten la variante por lista y la por grupo.</summary>
internal sealed class MoldeDePlantilla
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private readonly CuadroSemanalTurnos cuadro;

    private MoldeDePlantilla(CuadroSemanalTurnos cuadro) => this.cuadro = cuadro;

    public string Nombre => cuadro.Nombre;

    public static Task<(MoldeDePlantilla? Molde, string? Rechazo)> ResolverAsync(
        ResolutorPlantillaPorId resolutor, string plantillaId, MensajesDePlantilla mensajes, CancellationToken ct) =>
        throw new NotImplementedException();

    /// <summary>Una sola lectura del catalogo de turnos (CA-ADR-0038), solo cuando la cascada de sede la necesita.</summary>
    public async Task<(IReadOnlySet<Guid> Turnos, string? Fallo)> TurnosConFranjaSinSedeAsync(
        ProgramacionApi programacion, CancellationToken ct)
    {
        var respuesta = await programacion.ListarTurnos(ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return (new HashSet<Guid>(), fallo);

        var idsDelMolde = cuadro.Dias.Select(d => Guid.Parse(d.Turno.Id)).ToHashSet();
        var catalogo = (await respuesta.Content.ReadFromJsonAsync<SobreDeLista<FichaTurno>>(OpcionesLectura, ct))?.Elementos ?? [];
        return (catalogo
            .Select(TurnoAProgramar.De)
            .Where(t => t.TieneFranjaSinSede && idsDelMolde.Contains(t.Id))
            .Select(t => t.Id)
            .ToHashSet(), null);
    }

    public AsignacionDeTurno AsignacionDesde(DateOnly desde, IReadOnlySet<Guid> turnosConFranjaSinSede)
    {
        var asignacionDeFechas = AsignacionDePlantilla.Crear(cuadro, desde);
        return AsignacionDeTurno.PorFecha(fecha =>
        {
            var turno = asignacionDeFechas.Para(fecha);
            var id = Guid.Parse(turno.Id);
            return new TurnoAProgramar(id, turno.Nombre ?? string.Empty, turnosConFranjaSinSede.Contains(id));
        });
    }
}
