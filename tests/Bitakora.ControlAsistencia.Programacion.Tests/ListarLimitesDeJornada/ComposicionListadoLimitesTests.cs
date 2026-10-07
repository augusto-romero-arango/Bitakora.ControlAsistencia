using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarLimitesDeJornada;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarLimitesDeJornada;

public class ComposicionListadoLimitesTests
{
    private static readonly Guid IdA = Guid.Parse("00000000-0000-4000-8000-00000000000a");
    private static readonly Guid IdP = Guid.Parse("00000000-0000-4000-8000-00000000000b");
    private static readonly Guid IdB = Guid.Parse("00000000-0000-4000-8000-00000000000c");

    private static readonly LimitesDeJornada A = new(IdA.ToString(), 2400, 480, 0, 1, "40 h");
    private static readonly LimitesDeJornada P = new(IdP.ToString(), 2520, 480, 0, 1, "42 h");
    private static readonly LimitesDeJornada B = new(IdB.ToString(), 2880, 600, 0, 1, "48 h");

    [Fact]
    public void Componer_RespondeLaPredeterminada_CuandoLaVistaEstaVacia()
    {
        var respuesta = ComposicionListadoLimites.Componer([], P, null, null);

        respuesta.Elementos.Select(e => e.JornadaId).Should().Equal(IdP);
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_NoDuplicaLaPredeterminada_CuandoLaVistaYaLaTiene()
    {
        var respuesta = ComposicionListadoLimites.Componer([A, P, B], P, null, null);

        respuesta.Elementos.Select(e => e.JornadaId).Should().Equal(IdA, IdP, IdB);
    }

    [Fact]
    public void Componer_UbicaLaPredeterminadaEnSuPosicionDelOrden_CuandoLaVistaNoLaTiene()
    {
        var respuesta = ComposicionListadoLimites.Componer([A, B], P, null, null);

        respuesta.Elementos.Select(e => e.JornadaId).Should().Equal(IdA, IdP, IdB);
    }

    [Fact]
    public void Componer_RespetaLaPagina_CuandoLaVistaYaTieneLaPredeterminadaFueraDeEsaPagina()
    {
        var respuesta = ComposicionListadoLimites.Componer([A, B], null, null, 1);

        respuesta.Elementos.Select(e => e.JornadaId).Should().Equal(IdA);
        respuesta.SiguienteCursor.Should().NotBeNull();
    }

    [Fact]
    public void Componer_IncluyeLaPredeterminadaUnaSolaVez_CuandoSeRecorreEnVariasPaginas()
    {
        var vista = new[] { A, B };
        var ids = new List<Guid>();
        CursorJornada? cursor = null;

        for (var i = 0; i < 5; i++)
        {
            var pagina = ComposicionListadoLimites.Componer(vista, P, cursor, 1);
            ids.AddRange(pagina.Elementos.Select(e => e.JornadaId));
            if (pagina.SiguienteCursor is null) break;
            cursor = CursorJornada.Decodificar(pagina.SiguienteCursor);
        }

        ids.Should().Equal(IdA, IdP, IdB);
    }
}
