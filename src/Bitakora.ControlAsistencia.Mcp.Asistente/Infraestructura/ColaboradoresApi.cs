namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ColaboradoresApi(HttpClient http)
{
    public Task<HttpResponseMessage> ListarFichas(
        DateOnly fechaReferencia,
        string? codigoSede,
        IReadOnlyList<FiltroEtiqueta> etiquetas,
        int take,
        CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> ObtenerFicha(string identificacion, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> BuscarEnDirectorio(
        IReadOnlyList<string>? identificaciones, string? nombre, int take, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> Registrar(RegistroColaboradorSolicitado datos, CancellationToken ct) =>
        throw new NotImplementedException();
}

public sealed record FiltroEtiqueta(string Categoria, string Valor);

public sealed record RegistroColaboradorSolicitado(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string PrimerNombre,
    string? SegundoNombre,
    string PrimerApellido,
    string? SegundoApellido,
    string CodigoColaborador,
    DateOnly FechaInicio,
    string? CodigoSede);
