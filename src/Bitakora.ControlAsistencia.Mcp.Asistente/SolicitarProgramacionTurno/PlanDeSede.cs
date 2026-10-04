using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

/// <summary>Plantillas de motivo de aviso que cada tool aporta desde su .resx (MEF-ADR-0009).</summary>
internal sealed record MotivosDeAviso(string SinSede, string SedeInactiva, string SedeNoExiste);

public sealed record AvisoDeSede(string Identificacion, string Motivo);

/// <summary>Sede que viaja con la solicitud de un candidato y, si quedo sin sede, el motivo del aviso.</summary>
internal readonly record struct SedeDelCandidato(SedeProgramada? Sede, string? MotivoDeAviso);

// Cascada de sede por candidato, compartida por ambas tools (MEF-ADR-0018). La precedencia efectiva
// (sede prearmada de la franja primero) vive en TurnoProgramado.ConSedePorDefecto (MEF-ADR-0012);
// aqui solo se elige que default enviar.
internal sealed class PlanDeSede
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private readonly SedeProgramada? explicita;
    private readonly bool turnoTieneFranjaSinSede;
    private readonly IReadOnlyDictionary<string, FichaSede> maestro;
    private readonly MotivosDeAviso motivos;

    private PlanDeSede(
        SedeProgramada? explicita, bool turnoTieneFranjaSinSede,
        IReadOnlyDictionary<string, FichaSede> maestro, MotivosDeAviso motivos)
    {
        this.explicita = explicita;
        this.turnoTieneFranjaSinSede = turnoTieneFranjaSinSede;
        this.maestro = maestro;
        this.motivos = motivos;
    }

    public bool HaySedeExplicita => explicita is not null;

    /// <summary>Una sola lectura del maestro, sin filtro, y solo cuando la cascada la necesita.</summary>
    public static Task<(PlanDeSede? Plan, string? FalloDeLectura)> CrearAsync(
        SedesApi sedes, SedeProgramada? explicita, FichaTurno turno, MotivosDeAviso motivos, CancellationToken ct) =>
        CrearAsync(sedes, explicita, turno.Franjas.Any(f => f.SedeId is null), motivos, ct);

    /// <summary>Variante para varios turnos: sinFranjaSede si alguno tiene franjas sin sede prearmada.</summary>
    public static async Task<(PlanDeSede? Plan, string? FalloDeLectura)> CrearAsync(
        SedesApi sedes, SedeProgramada? explicita, bool sinFranjaSede, MotivosDeAviso motivos, CancellationToken ct)
    {
        IReadOnlyDictionary<string, FichaSede> maestro = new Dictionary<string, FichaSede>();

        if (explicita is null && sinFranjaSede)
        {
            var respuesta = await sedes.ListarFichas(ct);
            if (await respuesta.LeerFalloAsync(ct) is { } fallo)
                return (null, fallo);

            var fichas = await respuesta.Content.ReadFromJsonAsync<List<FichaSede>>(OpcionesLectura, ct) ?? [];
            maestro = fichas
                .GroupBy(f => f.Codigo, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        return (new PlanDeSede(explicita, sinFranjaSede, maestro, motivos), null);
    }

    public SedeDelCandidato Para(CandidatoProgramacion candidato)
    {
        if (explicita is not null)
            return new SedeDelCandidato(explicita, null);
        if (!turnoTieneFranjaSinSede)
            return new SedeDelCandidato(null, null);
        if (string.IsNullOrWhiteSpace(candidato.CodigoSede))
            return new SedeDelCandidato(null, motivos.SinSede);

        var codigo = candidato.CodigoSede.Trim();
        if (!maestro.TryGetValue(codigo, out var ficha))
            return new SedeDelCandidato(null, string.Format(motivos.SedeNoExiste, codigo));

        return ficha.Activa
            ? new SedeDelCandidato(new SedeProgramada(ficha.Codigo, ficha.Nombre, ficha.CentroDeCostos), null)
            : new SedeDelCandidato(null, string.Format(motivos.SedeInactiva, codigo));
    }
}
