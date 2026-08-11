// HU-348: guardrail de resolucion de los .resx de los VOs de identidad (MEF-ADR-0009).
//
// Por que existe: la clase Mensajes de cada VO devuelve ResourceManager.GetString(...)! -- el "!"
// es supresion del compilador, no garantia de runtime. Si la CLAVE desaparece del .resx (rename de
// la clave, merge que la pierde), GetString retorna null en silencio y los tests de validacion de
// este issue pasan en FALSO: sus aserciones son WithMessage($"*{Mensajes.X}*"), que con X == null
// se vuelve "**" y matchea cualquier excepcion.
//
// Verificado por mutacion durante la revision de #348: al renombrar la clave NumeroVacio en el
// .resx, los dos tests de Crear_LanzaArgumentException_CuandoNumeroEs* siguieron en VERDE y solo
// fallo este archivo. (El otro modo de falla -- romper el nombre logico del recurso -- si lanza
// MissingManifestResourceException y lo detecta cualquier test; el silencioso es este.)
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

public class MensajesResxTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenAIdentificacion()
    {
        Identificacion.Mensajes.NumeroVacio.Should().NotBeNullOrWhiteSpace();
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

    // HU-353: extiende el mismo guardrail a las claves de Etiqueta.
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenAEtiqueta()
    {
        Etiqueta.Mensajes.CategoriaVacia.Should().NotBeNullOrWhiteSpace();
        Etiqueta.Mensajes.ValorVacio.Should().NotBeNullOrWhiteSpace();
    }
}
