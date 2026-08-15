// Issue #357: segunda proyeccion concreta del dominio Colaboradores, PRIMERA receta N2
// (MultiStreamProjection<CategoriaDeEtiquetas, string>) de este BC. Invocacion DIRECTA de los
// metodos estaticos de CategoriaDeEtiquetasProjection (skills/projections/modelos-marten.md) -- no
// el DSL Given/When/Then de CommandHandlerTestBase (MEF-ADR-0002, testea command handlers contra el
// event store): aqui se testean funciones puras evento -> vista, sin abrir ningun stream.
//
// Create toma EtiquetaAsignada a secas, no IEvent<EtiquetaAsignada>: a diferencia de N1
// (FichaColaboradorProjectionTests, donde el Id del documento ES el StreamKey), aqui el Id lo fija
// el slicer de correlacion N2 (Identity<EtiquetaAsignada>(e => e.Etiqueta.CategoriaNormalizada)) --
// un campo del payload, no una propiedad de IEvent<T> (skills/projections/modelos-marten.md, "N2 --
// correlacion entre streams", nota sobre no generalizar el fix de N1 a N2).
//
// Cada assert compara contra un oraculo armado a mano (MEF-ADR-0002, no-tautologia): nunca se reusa
// la logica de Create/Apply bajo prueba para construir el valor esperado -- las formas normalizadas
// esperadas ("area", "tecnologia", ...) se escriben como literales, replicando a mano el algoritmo de
// Etiqueta.Normalizar (trim + remover diacriticos + minusculas), nunca invocando
// Etiqueta.NormalizarCategoria/CategoriaNormalizada para construir el oraculo (precedente
// FichaColaboradorProjectionTests).
//
// Dato de vistaPrevia en los tests de Apply: se construye a mano, simulando el documento que Marten
// ya habria materializado -- nunca se obtiene invocando Create/Apply, para no encadenar el oraculo
// de un test con la logica bajo prueba de otro.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Colaboradores;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;

namespace Bitakora.ControlAsistencia.Projections.Tests.Colaboradores;

public class CategoriaDeEtiquetasProjectionTests
{
    // --- CA-1: Create nace el documento con la categoria (display) y el primer valor ---

    [Fact]
    public void Create_ProyectaCategoriaYPrimerValor_DesdeEtiquetaAsignada()
    {
        var evento = new EtiquetaAsignada(Etiqueta.Crear("Área", "Tecnología"));

        var vista = CategoriaDeEtiquetasProjection.Create(evento);

        vista.Id.Should().Be("area");
        vista.Categoria.Should().Be("Área");
        vista.Valores.Should().BeEquivalentTo([new ValorCategoria("Tecnología", "tecnologia")]);
    }

    // --- CA-1/CA-3: test de correlacion N2 (obligatorio para MultiStreamProjection) -- dos eventos
    // que EN PRODUCCION vendrian de streams de DOS colaboradores distintos, con formas originales de
    // categoria distintas que normalizan igual ("Área" / "area"), convergen en el MISMO documento
    // (misma Id). No se abre ningun stream real: se invoca Create con el primero y Apply con el
    // segundo sobre la vista que dejo el primero -- asi es exactamente como el daemon de Marten
    // encadena eventos de streams distintos sobre un mismo documento N2 correlacionado. ---

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

    // --- CA-3 (segunda mitad): el display de la categoria refleja la ULTIMA asignacion, aunque la
    // forma original de la nueva asignacion sea distinta de la que nombro el documento ---

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

    // --- CA-2: reasignar el MISMO par (categoria, valor) no duplica ni la categoria (ya cubierto:
    // Apply siempre opera sobre el documento existente, nunca crea uno nuevo) ni el valor -- oraculo
    // mas fuerte que "un valor en la lista": descarta una implementacion que agregue sin upsert ---

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

    // --- CA-3 (valores): dos formas originales del MISMO valor normalizado son UN solo valor en el
    // catalogo; el display de ese valor refleja la ultima asignacion ---

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

    // --- CA-4: sobrescribir el valor de una categoria existente AGREGA el valor nuevo al catalogo y
    // CONSERVA el anterior -- acumulativo (decision de refinamiento 2026-08-13), a diferencia de
    // FichaColaborador (#356 CA-4), donde el mismo evento SI reemplaza el unico valor vigente de la
    // categoria. Oraculo mas fuerte que "contiene el valor nuevo": tambien exige el anterior ---

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

    // --- CA-1: dos categorias DISTINTAS (normalizadas distintas) nunca comparten documento -- cada
    // Create nace con Id propio, sin arrastrar los valores de otra categoria. Descarta una
    // implementacion que comparta estado global entre documentos distintos ---

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

    // Nota CA-5 (catalogo ACUMULATIVO, decision de refinamiento 2026-08-13): EtiquetaRetirada NO
    // descuenta nada del catalogo. No hay un test dedicado para esta regla -- la garantia es
    // estructural, no de comportamiento: CategoriaDeEtiquetasProjection (worker) simplemente no
    // declara ningun Apply/ShouldDelete para ese evento, asi que Marten no tiene que metodo
    // despachar. Un test que solo reflexionara sobre esa ausencia pasaria de una contra el stub
    // (nada que implementar lo pondria en rojo), violando el principio de la fase roja de este
    // agente -- por eso queda fuera del archivo, documentado aqui en vez de en un [Fact] verde
    // desde el commit inicial.
}
