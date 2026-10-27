using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarSedes;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests;

public class RemisionABusquedasTests
{
    private static readonly Assembly Servidor = typeof(ListarSedesTool).Assembly;

    private static readonly Regex Catalogos = new("listar_(turnos|sedes)", RegexOptions.Compiled);

    private static readonly Regex ConsistenciaEventual =
        new(@"(aparece|aparecer|refleja)\s+en\s+listar_(turnos|sedes)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string SinConsistenciaEventual(string texto) =>
        ConsistenciaEventual.Replace(Regex.Replace(texto, @"\s+", " "), string.Empty);

    private static IEnumerable<(string Tool, string Texto)> TextosDeTools()
    {
        var metodos = Servidor.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => m.GetParameters().Any(p => p.GetCustomAttribute<McpToolTriggerAttribute>() is not null));

        foreach (var metodo in metodos)
        {
            var trigger = metodo.GetParameters()
                .Select(p => p.GetCustomAttribute<McpToolTriggerAttribute>())
                .First(a => a is not null)!;
            yield return (trigger.ToolName, trigger.Description ?? string.Empty);
            foreach (var propiedad in metodo.GetParameters()
                .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
                .Where(a => a is not null))
                yield return (trigger.ToolName, propiedad!.Description ?? string.Empty);
        }
    }

    private static string Descripcion(string nombreTool) =>
        TextosDeTools().First(t => t.Tool == nombreTool).Texto;

    private static IEnumerable<(string Recurso, string Clave, string Texto)> MensajesResx()
    {
        foreach (var recurso in Servidor.GetManifestResourceNames().Where(n => n.EndsWith("ToolMensajes.resources")))
        {
            using var lector = new ResourceReader(Servidor.GetManifestResourceStream(recurso)!);
            foreach (System.Collections.DictionaryEntry entrada in lector)
                if (entrada.Value is string texto)
                    yield return (recurso, (string)entrada.Key, Regex.Replace(texto, @"\s+", " "));
        }
    }

    [Fact]
    public void DescripcionesDeTools_NoRemitenAListarParaUbicar_CuandoSeInspeccionaElServidor()
    {
        var infractores = TextosDeTools()
            .Where(t => !t.Tool.StartsWith("listar_") && !t.Tool.StartsWith("buscar_"))
            .Where(t => Catalogos.IsMatch(SinConsistenciaEventual(t.Texto)))
            .Select(t => $"{t.Tool}: {t.Texto}")
            .ToList();

        infractores.Should().BeEmpty();
        TextosDeTools().Count(t => t.Tool == "agregar_franja" && t.Texto.Contains("buscar_turnos"))
            .Should().BeGreaterThan(0);
    }

    [Fact]
    public void DescripcionesDeTools_RemitenALaBusquedaCorrespondiente_CuandoUbicanTurnoPlantillaOSede()
    {
        Descripcion("obtener_turno").Should().Contain("buscar_turnos");
        TextosDeTools().Where(t => t.Tool == "asignar_turno_a_dia_de_plantilla").Select(t => t.Texto)
            .Should().Contain(t => t.Contains("buscar_plantillas"))
            .And.Contain(t => t.Contains("buscar_turnos"));
        TextosDeTools().Where(t => t.Tool == "registrar_colaborador").Select(t => t.Texto)
            .Should().Contain(t => t.Contains("buscar_sedes"));
        foreach (var tool in new[] { "aplicar_plantilla_semanal", "aplicar_plantilla_semanal_por_grupo" })
            TextosDeTools().Where(t => t.Tool == tool).Select(t => t.Texto)
                .Should().Contain(t => t.Contains("buscar_plantillas"));
        TextosDeTools().Where(t => t.Tool == "crear_plantilla_semanal").Select(t => t.Texto)
            .Should().Contain(t => t.Contains("buscar_turnos"));
    }

    [Fact]
    public void MensajesResx_RemitenABuscarPlantillasYTurnos_CuandoElIdNoEsValidoONoExiste()
    {
        var mensajes = MensajesResx().ToList();

        foreach (var clave in new[] { "PlantillaIdInvalido", "PlantillaNoExiste" })
            mensajes.Where(m => m.Clave == clave && m.Recurso.Contains("AplicarPlantillaSemanal"))
                .Should().HaveCount(2).And.OnlyContain(m => m.Texto.Contains("buscar_plantillas"));
        foreach (var clave in new[] { "TurnoIdInvalido", "TurnosNoExisten", "TurnoPorNombreNoSoportado" })
            mensajes.Where(m => m.Clave == clave && m.Recurso.Contains("CrearPlantillaSemanal"))
                .Should().ContainSingle().Which.Texto.Should().Contain("buscar_turnos");
    }

    [Fact]
    public void MensajesResx_NoRemitenAListarParaUbicar_CuandoSeInspeccionanLosRecursos()
    {
        var infractores = MensajesResx()
            .Where(m => Catalogos.IsMatch(SinConsistenciaEventual(m.Texto)))
            .Select(m => $"{m.Recurso}/{m.Clave}: {m.Texto}")
            .ToList();

        infractores.Should().BeEmpty();
    }

    [Fact]
    public void MensajesResx_ConservanLaConsistenciaEventual_CuandoSeCreaOSeRetiraUnRecurso()
    {
        var mensajes = MensajesResx().Select(m => m.Texto).ToList();

        mensajes.Should().Contain(m => m.Contains("aparece en listar_turnos unos segundos"));
        mensajes.Should().Contain(m => m.Contains("aparece en listar_sedes en unos segundos"));
    }

    [Fact]
    public void DescripcionesDeTools_ConservanLaConsistenciaEventual_CuandoLaToolCreaUnRecurso()
    {
        Descripcion("crear_turno").Should().Contain("Aparece en listar_turnos en unos segundos");
        Regex.Replace(Descripcion("crear_plantilla_semanal"), @"\s+", " ")
            .Should().Contain("Aparece en listar_plantillas en unos segundos");
        Descripcion("registrar_sede").Should().Contain("aparece en listar_sedes en unos segundos");
    }

    [Fact]
    public void MensajesResx_RemitenABuscarSedes_CuandoLaSedeNoExiste()
    {
        var mensajes = MensajesResx().Select(m => m.Texto).Where(t => t.Contains("No existe una sede")).ToList();

        mensajes.Should().HaveCountGreaterThanOrEqualTo(7);
        mensajes.Should().OnlyContain(m => m.Contains("buscar_sedes"));
    }

    [Fact]
    public void Buscar_RemitenASuCatalogoParaTodosLosQueCumplenUnCriterio_CuandoSeInspeccionaSuDescripcion()
    {
        foreach (var (buscar, listar) in new[]
        {
            ("buscar_turnos", "listar_turnos"),
            ("buscar_sedes", "listar_sedes"),
        })
        {
            var texto = Regex.Replace(Descripcion(buscar), @"\s+", " ");
            texto.Should().Contain("Para ver todos los que cumplen un criterio", buscar);
            texto.Should().Contain($"usa {listar}", buscar);
            texto.Should().Contain("devuelve hasta 20 candidatos para ubicar uno", buscar);
            texto.Should().NotContain("catalogo completo", buscar);
        }
    }

    [Fact]
    public void Readme_ListaLasBusquedasEnLaTablaDeToolsPorRecurso_CuandoSeLee()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !Directory.Exists(Path.Combine(directorio.FullName, "src")))
            directorio = directorio.Parent;
        var readme = File.ReadAllText(Path.Combine(
            directorio!.FullName, "src", "Bitakora.ControlAsistencia.Mcp.Asistente", "README.md"));

        var filas = readme.Split('\n').Where(l => l.StartsWith("| Sedes") || l.StartsWith("| Turnos") || l.StartsWith("| Plantillas de turnos")).ToList();

        filas.Should().HaveCount(3);
        filas.Single(l => l.StartsWith("| Sedes")).Should().Contain("`buscar_sedes`");
        filas.Single(l => l.StartsWith("| Turnos")).Should().Contain("`buscar_turnos`");
        filas.Single(l => l.StartsWith("| Plantillas de turnos")).Should().Contain("`buscar_plantillas`");
    }
}
