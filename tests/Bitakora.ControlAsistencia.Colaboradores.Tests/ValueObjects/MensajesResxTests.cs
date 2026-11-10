// Guardrail: Mensajes.X es GetString(...)! y retorna null en silencio si la clave desaparece del .resx;
// los tests con WithMessage($"*{Mensajes.X}*") pasarian en falso porque el patron queda en "**".
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

public class MensajesResxTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenAIdentificacion()
    {
        Identificacion.Mensajes.NumeroVacio.Should().NotBeNullOrWhiteSpace();
        Identificacion.Mensajes.FormatoInvalido.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenATipoIdentificacion()
    {
        TipoIdentificacion.Mensajes.CodigoNoReconocido.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenANombreColaborador()
    {
        NombreColaborador.Mensajes.PrimerNombreRequerido.Should().NotBeNullOrWhiteSpace();
        NombreColaborador.Mensajes.PrimerApellidoRequerido.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenAEtiqueta()
    {
        Etiqueta.Mensajes.CategoriaVacia.Should().NotBeNullOrWhiteSpace();
        Etiqueta.Mensajes.ValorVacio.Should().NotBeNullOrWhiteSpace();
    }
}
