using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.BuscarSedes;

public partial class BuscarSedesTool(SedesApi api)
{
    internal const string NombreTool = "buscar_sedes";
    internal const int MaximoSedes = 20;

    [Function("BuscarSedes")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Busca sedes activas para saber a cual se refiere el usuario cuando la nombra de forma "
            + "aproximada. Por nombre o codigo, por ciudad o por direccion: palabras en cualquier "
            + "orden, sin importar acentos ni mayusculas, tambien incompletas ('chapi') o con una "
            + "letra de diferencia. Combina los criterios. Devuelve hasta 20 candidatas con su "
            + "codigo y el motivo de la coincidencia; si hay mas de una plausible, pregunta al "
            + "usuario antes de actuar. Para ver todos los que cumplen un criterio, usa "
            + "listar_sedes: esta busqueda devuelve hasta 20 candidatos para ubicar uno.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre o codigo de la sede, o parte de ellos, en cualquier orden.")]
        string? nombre,
        [McpToolProperty("ciudad", "Ciudad de la sede, o parte de ella.")]
        string? ciudad,
        [McpToolProperty("direccion", "Direccion de la sede, o parte de ella.")]
        string? direccion,
        CancellationToken ct)
    {
        var nombrePedido = Limpiar(nombre);
        var ciudadPedida = Limpiar(ciudad);
        var direccionPedida = Limpiar(direccion);

        if (nombrePedido is null && ciudadPedida is null && direccionPedida is null)
            return Mensajes.FaltaCriterio;

        var respuesta = await api.ListarFichasActivas(ct);
        respuesta.EnsureSuccessStatusCode();

        var fichas = (await respuesta.Content.ReadFromJsonAsync<SobreDeLista<FichaSede>>(ct))?.Elementos ?? [];

        var candidatos = fichas
            .Select(sede => Evaluar(sede, nombrePedido, ciudadPedida, direccionPedida))
            .OfType<Candidato>()
            .OrderByDescending(c => c.Nombre.Palabras)
            .ThenBy(c => c.Nombre.Calidad)
            .ThenByDescending(c => c.Ciudad.Palabras)
            .ThenBy(c => c.Ciudad.Calidad)
            .ThenByDescending(c => c.Direccion.Palabras)
            .ThenBy(c => c.Direccion.Calidad)
            .ThenBy(c => c.Sede.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidatos.Count == 0)
            return string.Format(
                Mensajes.SinCoincidencias, DescribirCriterios(nombrePedido, ciudadPedida, direccionPedida));

        var visibles = candidatos.Take(MaximoSedes).Select(c => c.Remodelar()).ToList();
        var nota = candidatos.Count > visibles.Count ? Mensajes.NotaTruncado : null;

        return RespuestaJson.Serializar(new CoincidenciasDeSedes(visibles.Count, visibles, nota));
    }

    private static Candidato? Evaluar(FichaSede sede, string? nombre, string? ciudad, string? direccion)
    {
        var motivos = new List<string>();
        var puntajeNombre = Puntaje.Ninguno;
        var puntajeCiudad = Puntaje.Ninguno;
        var puntajeDireccion = Puntaje.Ninguno;

        if (nombre is not null)
        {
            var porNombre = CoincidenciaDeTexto.Evaluar(nombre, sede.Nombre);
            var porCodigo = CoincidenciaDeTexto.Evaluar(nombre, sede.Codigo);
            if (porNombre.Count == 0 && porCodigo.Count == 0)
                return null;

            if (porNombre.Count > 0)
                motivos.Add(string.Format(Mensajes.CoincidePorNombre, Describir(porNombre)));
            if (porCodigo.Count > 0)
                motivos.Add(string.Format(Mensajes.CoincidePorCodigo, sede.Codigo));

            puntajeNombre = new[] { Puntaje.De(porNombre), Puntaje.De(porCodigo) }
                .OrderByDescending(p => p.Palabras)
                .ThenBy(p => p.Calidad)
                .First();
        }

        if (ciudad is not null)
        {
            var coincidencias = sede.Ciudad is null ? [] : CoincidenciaDeTexto.Evaluar(ciudad, sede.Ciudad);
            if (coincidencias.Count == 0)
                return null;

            puntajeCiudad = Puntaje.De(coincidencias);
            motivos.Add(string.Format(Mensajes.CoincidePorCiudad, Describir(coincidencias)));
        }

        if (direccion is not null)
        {
            var coincidencias = sede.Direccion is null ? [] : CoincidenciaDeTexto.Evaluar(direccion, sede.Direccion);
            if (coincidencias.Count == 0)
                return null;

            puntajeDireccion = Puntaje.De(coincidencias);
            motivos.Add(string.Format(Mensajes.CoincidePorDireccion, Describir(coincidencias)));
        }

        return new Candidato(sede, motivos, puntajeNombre, puntajeCiudad, puntajeDireccion);
    }

    private static string Describir(IReadOnlyList<PalabraCoincidente> coincidencias) =>
        string.Join(" ", coincidencias.Select(c => c.Tipo == TipoCoincidenciaDePalabra.Exacta
            ? c.Pedida
            : string.Format(Mensajes.PalabraAproximada, c.Pedida, c.Encontrada)));

    private static string DescribirCriterios(string? nombre, string? ciudad, string? direccion) =>
        string.Join(", ", new[]
        {
            nombre is null ? null : string.Format(Mensajes.CriterioNombre, nombre),
            ciudad is null ? null : string.Format(Mensajes.CriterioCiudad, ciudad),
            direccion is null ? null : string.Format(Mensajes.CriterioDireccion, direccion)
        }.OfType<string>());

    private static string? Limpiar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private readonly record struct Puntaje(int Palabras, int Calidad)
    {
        public static Puntaje Ninguno => new(0, 0);

        public static Puntaje De(IReadOnlyList<PalabraCoincidente> coincidencias) =>
            new(coincidencias.Count, coincidencias.Sum(c => (int)c.Tipo));
    }

    private sealed record Candidato(
        FichaSede Sede, List<string> Motivos, Puntaje Nombre, Puntaje Ciudad, Puntaje Direccion)
    {
        public SedeEncontrada Remodelar() => new(Sede.Codigo, Sede.Nombre, Sede.Ciudad, Sede.Direccion, Motivos);
    }
}

public sealed record CoincidenciasDeSedes(
    int Mostrando,
    IReadOnlyList<SedeEncontrada> Sedes,
    string? Nota);

public sealed record SedeEncontrada(
    string Codigo,
    string Nombre,
    string? Ciudad,
    string? Direccion,
    IReadOnlyList<string> CoincidePor);
