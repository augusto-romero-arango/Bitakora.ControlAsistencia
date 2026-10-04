using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.SolicitarProgramacionTurno;

public class EjecutorDeProgramacionVariosTurnosTests
{
    private const string RutaSolicitudes = "/api/programacion/solicitudes";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);
    private static readonly TurnoAProgramar Manana = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Manana", false);
    private static readonly TurnoAProgramar Tarde = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Tarde", false);
    private static readonly SedeProgramada Sede = new("SUBA", "Suba", null);

    private static readonly FichaTurno FichaSinFranjas = new("x", "x", false, "", [], "", true);

    private static CandidatoProgramacion Candidato(
        string identificacion, string vigenteDesde = "2025-01-01", string? vigenteHasta = null) =>
        new(identificacion, "C" + identificacion, "Colab " + identificacion,
            DateOnly.Parse(vigenteDesde), vigenteHasta is null ? null : DateOnly.Parse(vigenteHasta));

    private static DateOnly Dia(int dia) => new(2026, 9, dia);

    // Lunes a miercoles: Manana; jueves en adelante: Tarde.
    private static AsignacionDeTurno MananaHastaElDia3() =>
        AsignacionDeTurno.PorFecha(f => f.Day <= 3 ? Manana : Tarde);

    private static async Task<(ResultadoEjecucion Resultado, List<SolicitudProgramacionTurno> Posts, ContadoresDeEjecucion Contadores)>
        Ejecutar(
            IReadOnlyList<CandidatoProgramacion> candidatos,
            AsignacionDeTurno asignacion,
            Func<SolicitudProgramacionTurno, HttpResponseMessage>? responder = null)
    {
        var posts = new List<SolicitudProgramacionTurno>();
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Post, RutaSolicitudes, (_, cuerpo) =>
        {
            var solicitud = JsonSerializer.Deserialize<SolicitudProgramacionTurno>(cuerpo!, OpcionesJson)!;
            lock (posts)
                posts.Add(solicitud);
            return responder?.Invoke(solicitud)
                ?? new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("") };
        });

        var (clienteSedes, _) = ClienteFalso.Con("[]");
        var plan = (await PlanDeSede.CrearAsync(
            new SedesApi(clienteSedes), Sede, FichaSinFranjas,
            new MotivosDeAviso("s", "i", "n"), CancellationToken.None)).Plan!;
        var contadores = new ContadoresDeEjecucion();
        var resultado = await EjecutorDeProgramacion.EjecutarAsync(
            new ProgramacionApi(cliente), candidatos, asignacion, plan,
            VentanaDeProgramacion.Crear(Dia(1), Dia(5)), contadores, CancellationToken.None);
        return (resultado, posts, contadores);
    }

    [Fact]
    public async Task EjecutarAsync_HaceUnPostPorTurnoConSusFechas_CuandoLaAsignacionTieneDosTurnos()
    {
        var (resultado, posts, _) = await Ejecutar([Candidato("CC-1")], MananaHastaElDia3());

        posts.Should().HaveCount(2);
        posts.Single(p => p.TurnoId == Manana.Id).Fechas.Should().Equal(Dia(1), Dia(2), Dia(3));
        posts.Single(p => p.TurnoId == Tarde.Id).Fechas.Should().Equal(Dia(4), Dia(5));
        resultado.Programados.Should().ContainSingle();
        resultado.Programados[0].Desde.Should().Be(Dia(1));
        resultado.Programados[0].Hasta.Should().Be(Dia(5));
        resultado.Programados[0].Dias.Should().Be(5);
        resultado.Fallidos.Should().BeNull();
    }

    [Fact]
    public async Task EjecutarAsync_ListaAlColaboradorEnProgramadosYFallidos_CuandoSoloUnPostSeRechaza()
    {
        var (resultado, _, contadores) = await Ejecutar(
            [Candidato("CC-1")], MananaHastaElDia3(),
            s => s.TurnoId == Tarde.Id
                ? new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("choca", Encoding.UTF8) }
                : new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("") });

        resultado.Programados.Should().ContainSingle();
        resultado.Programados[0].Dias.Should().Be(3);
        resultado.Programados[0].Hasta.Should().Be(Dia(3));
        var fallido = resultado.Fallidos.Should().ContainSingle().Subject;
        fallido.Identificacion.Should().Be("CC-1");
        fallido.Motivo.Should().Be("choca");
        fallido.Turno.Should().Be("Tarde");
        contadores.Programados.Should().Be(1);
        contadores.Fallidos.Should().Be(1);
    }

    [Fact]
    public async Task EjecutarAsync_NoCambiaElTurnoDeLosDiasRestantes_CuandoLaVigenciaRecortaDias()
    {
        var (resultado, posts, _) = await Ejecutar(
            [Candidato("CC-1", vigenteDesde: "2026-09-03", vigenteHasta: "2026-09-04")], MananaHastaElDia3());

        posts.Should().HaveCount(2);
        posts.Single(p => p.TurnoId == Manana.Id).Fechas.Should().Equal(Dia(3));
        posts.Single(p => p.TurnoId == Tarde.Id).Fechas.Should().Equal(Dia(4));
        resultado.Programados[0].Dias.Should().Be(2);
    }

    [Fact]
    public async Task EjecutarAsync_OmiteElTurnoDelFallido_CuandoLaAsignacionUsaUnSoloTurno()
    {
        var (resultado, posts, _) = await Ejecutar(
            [Candidato("CC-1")], AsignacionDeTurno.UnSoloTurno(Manana),
            _ => new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("choca") });

        posts.Should().ContainSingle().Which.Fechas.Should().HaveCount(5);
        resultado.Programados.Should().BeEmpty();
        resultado.Fallidos.Should().ContainSingle().Which.Turno.Should().BeNull();
    }

    [Fact]
    public async Task EjecutarAsync_CuentaOmitido_CuandoElCandidatoNoTieneDiasCubiertos()
    {
        var (resultado, posts, contadores) = await Ejecutar(
            [Candidato("CC-1", vigenteDesde: "2027-01-01")], MananaHastaElDia3());

        posts.Should().BeEmpty();
        resultado.Omitidos.Should().Be(1);
        contadores.Omitidos.Should().Be(1);
        resultado.Programados.Should().BeEmpty();
    }

    [Fact]
    public async Task EjecutarAsync_CuentaUnSoloFallido_CuandoSeRechazanLosPostDeAmbosTurnos()
    {
        var (resultado, _, contadores) = await Ejecutar(
            [Candidato("CC-1")], MananaHastaElDia3(),
            s => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(s.TurnoId == Manana.Id ? "choca manana" : "choca tarde"),
            });

        resultado.Programados.Should().BeEmpty();
        resultado.Fallidos.Should().HaveCount(2);
        resultado.Fallidos!.Select(f => (f.Turno, f.Motivo)).Should().Equal(
            ("Manana", "choca manana"), ("Tarde", "choca tarde"));
        contadores.Fallidos.Should().Be(1);
        contadores.Programados.Should().Be(0);
    }

    [Fact]
    public async Task EjecutarAsync_CombinaLosDiasRespetadosEntreTurnos_CuandoAmbosPostRespetanAusencias()
    {
        var (resultado, _, _) = await Ejecutar(
            [Candidato("CC-1")], MananaHastaElDia3(),
            s => new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(s.TurnoId == Manana.Id
                    ? """{"fechasRespetadas":[{"fecha":"2026-09-03","motivo":"Vacaciones"}]}"""
                    : """{"fechasRespetadas":[{"fecha":"2026-09-04","motivo":"Vacaciones"}]}"""),
            });

        var programado = resultado.Programados.Should().ContainSingle().Subject;
        programado.Dias.Should().Be(3);
        programado.Respetados.Should().ContainSingle()
            .Which.Should().Be(new DiasRespetadosResumen("Vacaciones", "3-4"));
    }
}
