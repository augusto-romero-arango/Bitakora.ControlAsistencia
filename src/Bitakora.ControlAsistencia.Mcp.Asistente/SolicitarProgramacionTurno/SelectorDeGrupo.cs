using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

public sealed record MensajesDeSelector(
    string SelectorObligatorio, string EtiquetaMalFormada, string SedeNoExiste, string SedeInactiva,
    string RechazoDelDominio);

public sealed record SelectorDeGrupo(string? CodigoSede, IReadOnlyList<FiltroEtiqueta> Filtros)
{
    public string Descripcion => string.Join(
        ", ",
        (CodigoSede is null ? [] : new[] { $"sede:{CodigoSede}" })
            .Concat(Filtros.Select(f => $"{f.Categoria}:{f.Valor}")));

    public string? EtiquetasComoTexto =>
        Filtros.Count == 0 ? null : string.Join(", ", Filtros.Select(f => $"{f.Categoria}:{f.Valor}"));

    public static async Task<(SelectorDeGrupo? Selector, string? Rechazo)> ResolverAsync(
        string? sede, string? etiquetas, ResolutorSedePorCodigo resolutorSedes,
        MensajesDeSelector mensajes, CancellationToken ct)
    {
        var codigoSede = string.IsNullOrWhiteSpace(sede) ? null : sede.Trim();
        var pares = (etiquetas ?? string.Empty)
            .Split(',')
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();
        if (codigoSede is null && pares.Count == 0)
            return (null, mensajes.SelectorObligatorio);

        var filtros = new List<FiltroEtiqueta>();
        foreach (var par in pares)
        {
            var partes = par.Split(':', 2);
            if (partes.Length < 2 || partes[0].Trim().Length == 0 || partes[1].Trim().Length == 0)
                return (null, string.Format(mensajes.EtiquetaMalFormada, par));
            filtros.Add(new FiltroEtiqueta(partes[0].Trim(), partes[1].Trim()));
        }

        string? codigoCanonico = null;
        if (codigoSede is not null)
        {
            var resolucion = await resolutorSedes.ResolverAsync(codigoSede, ct);
            if (resolucion.FalloDeLectura is { } fallo)
                return (null, string.Format(mensajes.RechazoDelDominio, fallo));
            if (resolucion.MensajeDelMotivo(
                codigoSede, noExiste: mensajes.SedeNoExiste, inactiva: mensajes.SedeInactiva) is { } rechazo)
                return (null, rechazo);
            codigoCanonico = resolucion.Sede!.Id;
        }

        return (new SelectorDeGrupo(codigoCanonico, filtros), null);
    }
}
