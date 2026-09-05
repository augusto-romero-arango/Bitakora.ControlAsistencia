// Issue #621 CA-1: DiaSemana es una lista cerrada de 7 instancias canonicas (Lunes..Domingo,
// numero ISO 8601) -- mismo patron que TipoIdentificacion (Colaboradores.DomainEvents).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class DiaSemanaTests
{
    [Fact]
    public void Desde_RetornaInstanciaLunes_CuandoNumeroEsUno()
    {
        var resultado = DiaSemana.Desde(1);

        resultado.Should().BeSameAs(DiaSemana.Lunes);
        resultado.Numero.Should().Be(1);
    }

    [Fact]
    public void Desde_RetornaInstanciaDomingo_CuandoNumeroEsSiete()
    {
        var resultado = DiaSemana.Desde(7);

        resultado.Should().BeSameAs(DiaSemana.Domingo);
        resultado.Numero.Should().Be(7);
    }

    [Fact]
    public void Desde_RetornaInstanciaViernes_CuandoNumeroEsCinco()
    {
        var resultado = DiaSemana.Desde(5);

        resultado.Should().BeSameAs(DiaSemana.Viernes);
        resultado.Numero.Should().Be(5);
    }

    // CA-1: la misma instancia canonica en llamadas repetidas -- Desde() nunca crea una nueva.
    [Fact]
    public void Desde_RetornaLaMismaInstanciaCanonica_CuandoSeLlamaDosVecesConElMismoNumero()
    {
        var primera = DiaSemana.Desde(3);
        var segunda = DiaSemana.Desde(3);

        primera.Should().BeSameAs(segunda);
    }

    [Fact]
    public void Desde_LanzaArgumentException_CuandoNumeroEsCero()
    {
        var act = () => DiaSemana.Desde(0);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{DiaSemana.Mensajes.NumeroFueraDeRango}*");
    }

    [Fact]
    public void Desde_LanzaArgumentException_CuandoNumeroEsOcho()
    {
        var act = () => DiaSemana.Desde(8);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{DiaSemana.Mensajes.NumeroFueraDeRango}*");
    }

    [Fact]
    public void Desde_LanzaArgumentException_CuandoNumeroEsNegativo()
    {
        var act = () => DiaSemana.Desde(-1);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{DiaSemana.Mensajes.NumeroFueraDeRango}*");
    }
}
