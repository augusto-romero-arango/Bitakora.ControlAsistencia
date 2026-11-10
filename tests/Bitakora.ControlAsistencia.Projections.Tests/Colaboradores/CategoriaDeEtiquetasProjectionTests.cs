// Invocacion DIRECTA de los metodos estaticos de la proyeccion N2 (MultiStreamProjection), no el DSL
// Given/When/Then: aqui se testean funciones puras evento -> vista, sin abrir ningun stream.
//
// Create toma EtiquetaAsignada a secas, no IEvent<EtiquetaAsignada>: a diferencia de N1 (donde el Id
// del documento ES el StreamKey), aqui el Id lo fija el slicer de correlacion
// (Identity<EtiquetaAsignada>(e => e.Etiqueta.CategoriaNormalizada)), un campo del payload. No
// generalizar el fix de N1 a N2 (skills/projections/modelos-marten.md, "N2 -- correlacion entre streams").
//
// Oraculo armado a mano: las formas normalizadas esperadas ("area", "tecnologia", ...) son literales,
// nunca salen de Etiqueta.NormalizarCategoria/CategoriaNormalizada; la vistaPrevia de los tests de Apply
// se construye a mano, nunca con Create/Apply, para no encadenar el oraculo de un test con la logica
// bajo prueba de otro.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Colaboradores;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;

namespace Bitakora.ControlAsistencia.Projections.Tests.Colaboradores;

public class CategoriaDeEtiquetasProjectionTests
{
    [Fact]
    public void Create_ProyectaCategoriaYPrimerValor_DesdeEtiquetaAsignada()
    {
        var evento = new EtiquetaAsignada(Etiqueta.Crear("Área", "Tecnología"));

        var vista = CategoriaDeEtiquetasProjection.Create(evento);

        vista.Id.Should().Be("area");
        vista.Categoria.Should().Be("Área");
        vista.Valores.Should().BeEquivalentTo([new ValorCategoria("Tecnología", "tecnologia")]);
    }

    // Test de correlacion N2 (obligatorio para MultiStreamProjection): en produccion los dos eventos
    // vendrian de streams de DOS colaboradores, con formas de categoria que normalizan igual
    // ("Área" / "area"). Create con el primero y Apply con el segundo sobre la vista que dejo el
    // primero es exactamente como el daemon encadena streams distintos sobre un documento N2.
    [Fact]
    public void Create_y_Apply_CorrelacionanPorCategoriaNormalizada_DesdeDosStreamsDeColaboradorDistintos()
    {
        var eventoDeColaboradorA = new EtiquetaAsignada(Etiqueta.Crear("Área", "Tecnología"));
        var eventoDeColaboradorB = new EtiquetaAsignada(Etiqueta.Crear("area", "Sistemas"));

        var vistaTrasColaboradorA = CategoriaDeEtiquetasProjection.Create(eventoDeColaboradorA);
        var vistaTrasColaboradorB = CategoriaDeEtiquetasProjection.Apply(eventoDeColaboradorB, vistaTrasColaboradorA);

        vistaTrasColaboradorA.Id.Should().Be("area");
        vistaTrasColaboradorB.Id.Should().Be("area");
    }

    [Fact]
    public void Apply_ActualizaElDisplayDeLaCategoria_CuandoLaNuevaAsignacionUsaOtraFormaOriginal()
    {
        var vistaPrevia = new CategoriaDeEtiquetas(
            "area", "Área", [new ValorCategoria("Tecnología", "tecnologia")]);
        var evento = new EtiquetaAsignada(Etiqueta.Crear("area", "Tecnología"));

        var vista = CategoriaDeEtiquetasProjection.Apply(evento, vistaPrevia);

        vista.Id.Should().Be("area");
        vista.Categoria.Should().Be("area");
    }

    // Oraculo mas fuerte que "un valor en la lista": descarta una implementacion que agregue sin upsert.
    [Fact]
    public void Apply_NoDuplicaElValor_CuandoSeReasignaElMismoParCategoriaValor()
    {
        var vistaPrevia = new CategoriaDeEtiquetas(
            "area", "Área", [new ValorCategoria("Tecnología", "tecnologia")]);
        var evento = new EtiquetaAsignada(Etiqueta.Crear("Área", "Tecnología"));

        var vista = CategoriaDeEtiquetasProjection.Apply(evento, vistaPrevia);

        vista.Valores.Should().HaveCount(1);
        vista.Valores.Should().BeEquivalentTo([new ValorCategoria("Tecnología", "tecnologia")]);
    }

    // Dos formas originales del MISMO valor normalizado son UN solo valor en el catalogo.
    [Fact]
    public void Apply_ActualizaElDisplayDelValor_CuandoDosFormasOriginalesDelValorNormalizanIgual()
    {
        var vistaPrevia = new CategoriaDeEtiquetas(
            "area", "Área", [new ValorCategoria("Tecnología", "tecnologia")]);
        var evento = new EtiquetaAsignada(Etiqueta.Crear("area", "tecnologia"));

        var vista = CategoriaDeEtiquetasProjection.Apply(evento, vistaPrevia);

        vista.Valores.Should().HaveCount(1);
        vista.Valores.Should().BeEquivalentTo([new ValorCategoria("tecnologia", "tecnologia")]);
    }

    // El catalogo es acumulativo, a diferencia de FichaColaborador, donde el mismo evento SI reemplaza
    // el unico valor vigente de la categoria.
    [Fact]
    public void Apply_AgregaElValorNuevoYConservaElAnterior_CuandoSeSobrescribeElValorDeLaCategoria()
    {
        var vistaPrevia = new CategoriaDeEtiquetas(
            "area", "Área", [new ValorCategoria("Tecnología", "tecnologia")]);
        var evento = new EtiquetaAsignada(Etiqueta.Crear("Área", "Sistemas"));

        var vista = CategoriaDeEtiquetasProjection.Apply(evento, vistaPrevia);

        vista.Valores.Should().BeEquivalentTo(
        [
            new ValorCategoria("Tecnología", "tecnologia"),
            new ValorCategoria("Sistemas", "sistemas")
        ]);
    }

    // Descarta una implementacion que comparta estado global entre documentos distintos.
    [Fact]
    public void Create_NaceUnDocumentoIndependiente_ParaCadaCategoriaNormalizadaDistinta()
    {
        var eventoArea = new EtiquetaAsignada(Etiqueta.Crear("Área", "Tecnología"));
        var eventoSede = new EtiquetaAsignada(Etiqueta.Crear("Sede", "Suba"));

        var vistaArea = CategoriaDeEtiquetasProjection.Create(eventoArea);
        var vistaSede = CategoriaDeEtiquetasProjection.Create(eventoSede);

        vistaArea.Id.Should().Be("area");
        vistaArea.Valores.Should().BeEquivalentTo([new ValorCategoria("Tecnología", "tecnologia")]);
        vistaSede.Id.Should().Be("sede");
        vistaSede.Valores.Should().BeEquivalentTo([new ValorCategoria("Suba", "suba")]);
    }

    // EtiquetaRetirada NO descuenta nada del catalogo (ACUMULATIVO). Sin test dedicado: la garantia es
    // estructural (la proyeccion no declara Apply/ShouldDelete para ese evento) y un test que solo
    // reflexionara sobre esa ausencia nunca habria estado en rojo.
}
