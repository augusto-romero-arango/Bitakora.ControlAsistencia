// Issue #357: smoke tests de ListarCategoriasDeEtiquetas, GET colaboradores/etiquetas/categorias --
// Function GET read-side (opcion B, decision de refinamiento: catalogo COMPLETO de un tiro, sin
// filtros ni paginacion) sobre la proyeccion CategoriaDeEtiquetas (receta N2,
// MultiStreamProjection<CategoriaDeEtiquetas, string>, MEF-ADR-0034/0035): la PRIMERA N2 de este BC
// -- eventos EtiquetaAsignada de MUCHOS streams de ColaboradorAggregateRoot convergen en el MISMO
// documento cuando comparten categoria normalizada.
//
// Arrange via API, nunca sembrando el event store por fuera de ella: cada colaborador se crea con
// POST Colaboradores (#330) y se etiqueta/retira con POST Colaboradores/Etiquetas y
// Colaboradores/Etiquetas/Retiros (#355) -- los mismos comandos que la proyeccion consume.
//
// Sin ObtenerCategoriaDeEtiquetas por id en este issue (el documento ya tiene id direccionable,
// pero se agrega solo si un caso real lo pide -- Rule of Three, MEF-ADR-0018): por eso este archivo
// SOLO cubre el caso "Listado" de la doctrina read-side (skills/projections/read-apis.md), nunca
// "recurso existente"/"recurso no encontrado" via id de ruta -- este endpoint no toma ningun
// segmento de ruta ni body, asi que tampoco hay casos de validacion 400/422 que probar.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa/actualiza CategoriaDeEtiquetas
// DESPUES de que Colaboradores persiste sus eventos. Los casos de exito envuelven la consulta en
// Polling.WaitUntilAsync (timeout estandar 30s) -- unica excepcion documentada al "no usar Polling
// directo en tests".
//
// Aislamiento SIN cleanup, en un entorno que ACUMULA categorias de corridas anteriores (el catalogo
// es GLOBAL y ACUMULATIVO por diseno, decision de refinamiento 2026-08-13: nada sale nunca): un
// listado completo sin filtros nunca es un oraculo confiable por si solo. Cada test aisla SU propia
// categoria embebiendo un Guid en el texto de la categoria misma (formato "N": hexadecimal en
// minusculas, sobrevive intacto a la normalizacion) -- nadie mas, en ninguna corrida pasada o
// futura, puede tener esa categoria normalizada exacta. Se filtra siempre por Id (la categoria
// normalizada), nunca por posicion/indice.
//
// Patron "checkpoint"/evento centinela (CA-2/CA-3/CA-5): para verificar una condicion NEGATIVA
// ("no duplica", "colapsa a un solo valor", "el retiro no descuenta") contra una proyeccion
// asincrona, no basta con esperar a que aparezca el primer efecto -- hace falta certeza de que
// TODOS los eventos previos de la corrida ya fueron aplicados. Como el async daemon de Marten
// aplica los eventos en el orden de su secuencia global (mt_events), y este smoke test emite sus
// comandos secuencialmente (await cada POST antes del siguiente), un evento "centinela" (una
// asignacion en una categoria discriminadora final, unica para el checkpoint) que ya aparece en el
// catalogo GARANTIZA que todos los eventos anteriores de esta corrida ya se aplicaron. Es la unica
// forma deterministica de probar una ausencia bajo consistencia eventual, sin recurrir a un
// Task.Delay arbitrario.
//
// Formas discriminadoras SIN tildes (mayusculas vs minusculas simples, no diacriticos): el detalle
// Unicode de la normalizacion (NonSpacingMark, ver el comentario de Etiqueta.Normalizar) ya lo
// prueban el unit test del VO y AsignarEtiquetaSmokeTests (CA-2, "Área"/"area"); repetirlo aqui solo
// arriesgaria un bug de replicacion en el propio smoke test sin agregar cobertura nueva sobre el
// EFECTO end-to-end que este archivo verifica.
//
// No se repite aqui el detalle exhaustivo de Create/Apply (agrupacion, upsert por
// ValorNormalizado, ausencia de metodo para EtiquetaRetirada): eso ya lo cubre el unit test de
// CategoriaDeEtiquetasProjection (projection-test-writer). Este smoke test es black-box: solo
// verifica que el endpoint desplegado expone la vista materializada real, contra el entorno real.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.ListarCategoriasDeEtiquetas;

public class ListarCategoriasDeEtiquetasSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaCatalogo = "/api/colaboradores/etiquetas/categorias";
    private const string RutaRegistrar = "/api/Colaboradores";
    private const string RutaEtiquetas = "/api/Colaboradores/Etiquetas";
    private const string RutaRetiros = "/api/Colaboradores/Etiquetas/Retiros";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase), mientras que las formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Formas locales DESACOPLADAS del read model de produccion
    // (Bitakora.ControlAsistencia.ReadModels.Colaboradores.CategoriaDeEtiquetas/ValorCategoria): el
    // smoke test no referencia ReadModels (isla, MEF-ADR-0034 seccion 5) ni el worker de
    // proyecciones. Replican solo el shape JSON de la respuesta HTTP -- la vista se serializa tal
    // cual, sin DTO de respuesta (ver el comentario del propio FunctionEndpoint, MEF-ADR-0041
    // decision 4).
    private sealed record ValorCategoriaSmoke(string Valor, string ValorNormalizado);

    private sealed record CategoriaDeEtiquetasSmoke(
        string Id, string Categoria, IReadOnlyList<ValorCategoriaSmoke> Valores);

    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Issue #387: codigo URL-safe (unreserved RFC 3986) -- corregido de "[TEST]-" (corchetes
    // fuera del set permitido) a "TEST-" para que el arrange no falle con 400.
    private static string NuevoCodigoColaborador() => $"TEST-{Guid.CreateVersion7()}";

    // Formato "N": hexadecimal en minusculas -- sobrevive intacto a la normalizacion del VO Etiqueta
    // (minusculas + sin tildes + trim). Lo unico que varia entre "formas" de una misma categoria
    // discriminadora es el prefijo alrededor de este sufijo unico.
    private static string NuevoSufijoUnico() => Guid.CreateVersion7().ToString("N");

    // Arrange comun: registra un colaborador con una vinculacion abierta -- via el comando que la
    // origina (#330), nunca sembrando el event store por fuera del API.
    private async Task RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(RutaRegistrar, new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            primerNombre = "[TEST]",
            segundoNombre = (string?)null,
            primerApellido = "Smoke",
            segundoApellido = (string?)null,
            codigoColaborador = NuevoCodigoColaborador(),
            fechaInicio
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");
    }

    // Arrange comun: asigna una etiqueta dinamica -- via el comando que la origina (#355), la MISMA
    // fuente de eventos que alimenta CategoriaDeEtiquetasProjection.
    private async Task AsignarEtiquetaAsync(
        string numeroIdentificacion, string categoria, string valor, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(RutaEtiquetas, new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            categoria,
            valor
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que AsignarEtiqueta funcione");
    }

    // Arrange comun (CA-5): retira una etiqueta dinamica -- via el comando que la origina (#355).
    private async Task RetirarEtiquetaAsync(
        string numeroIdentificacion, string categoria, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(RutaRetiros, new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            categoria
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RetirarEtiqueta funcione");
    }

    private async Task<List<CategoriaDeEtiquetasSmoke>> ObtenerCatalogoAsync(CancellationToken ct)
    {
        var response = await _client.GetAsync(RutaCatalogo, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalogo = await response.Content.ReadFromJsonAsync<List<CategoriaDeEtiquetasSmoke>>(
            JsonOptions, cancellationToken: ct);
        return catalogo ?? [];
    }

    // Reintenta el GET hasta que la proyeccion asincrona satisfaga la condicion -- unica excepcion
    // documentada al "no usar Polling directo en tests" (lifecycle Async, MEF-ADR-0034 seccion 3).
    // Si el timeout se agota es un fallo real (worker no desplegado, proyeccion sin registrar en el
    // named store, lifecycle equivocado), nunca un caso para Assert.Skip -- Polling.WaitUntilAsync
    // lanza TimeoutException.
    private Task<List<CategoriaDeEtiquetasSmoke>> EsperarCatalogoAsync(
        Func<List<CategoriaDeEtiquetasSmoke>, bool> condicion, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            var catalogo = await ObtenerCatalogoAsync(ct);
            return condicion(catalogo) ? catalogo : null;
        }, Timeout);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-6 (mitad observable black-box): el GET siempre responde 200 con una coleccion, nunca 404
    // -- una lista vacia es una respuesta valida, no un recurso ausente. El catalogo COMPLETAMENTE
    // vacio (cero categorias en todo el tenant) solo es observable en un entorno recien
    // provisionado; en dev, compartido y acumulativo por diseno, este smoke test no puede forzar esa
    // condicion sin borrar datos de otras corridas (fuera de alcance de un smoke test).
    //
    // La otra mitad de CA-6 -- que la respuesta sea una coleccion VACIA y no un 404 cuando el
    // tenant todavia no tiene ninguna categoria -- no tiene test propio en ningun nivel, y es
    // deliberado: session.Query<T>() sobre una tabla sin filas devuelve lista vacia por
    // construccion, y un unit test del endpoint solo verificaria ese comportamiento de Marten con
    // un doble. Su guardrail real es el test de composicion del contenedor
    // (ComposicionServiciosTests.AgregarServiciosColaboradores_ResuelveElEndpointDeListarCategorias
    // DeEtiquetas...) mas este smoke test, que si afirma el 200 contra el entorno real.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_Retorna200ConUnaColeccion_Siempre()
    {
        var ct = TestContext.Current.CancellationToken;

        var catalogo = await ObtenerCatalogoAsync(ct);

        catalogo.Should().NotBeNull();
    }

    // CA-1: etiquetas asignadas en VARIOS colaboradores -> el GET devuelve las categorias
    // DISTINTAS, cada una con sus valores DISTINTOS, en forma display. Dos categorias
    // discriminadoras (Guid embebido) demuestran que la proyeccion agrupa por categoria
    // NORMALIZADA a traves de streams distintos (receta N2, correlacion), sin mezclar los valores
    // de una categoria con los de la otra.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_DevuelveCategoriasDistintasConSusValores_CuandoVariosColaboradoresAsignanEtiquetasDistintas()
    {
        var ct = TestContext.Current.CancellationToken;
        var categoriaA = $"area357-{NuevoSufijoUnico()}";
        var categoriaB = $"cargo357-{NuevoSufijoUnico()}";
        var valorA = "tecnologia";
        var valorB = "desarrollador";

        var numeroA = NuevoNumeroIdentificacion();
        var numeroB = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroA, new DateOnly(2026, 1, 1), ct);
        await RegistrarColaboradorAsync(numeroB, new DateOnly(2026, 1, 1), ct);
        await AsignarEtiquetaAsync(numeroA, categoriaA, valorA, ct);
        await AsignarEtiquetaAsync(numeroB, categoriaB, valorB, ct);

        var catalogo = await EsperarCatalogoAsync(
            lista => lista.Any(c => c.Id == categoriaA) && lista.Any(c => c.Id == categoriaB), ct);

        var vistaA = catalogo.Should().ContainSingle(c => c.Id == categoriaA).Subject;
        vistaA.Categoria.Should().Be(categoriaA);
        vistaA.Valores.Should().ContainSingle(v => v.ValorNormalizado == valorA)
            .Which.Valor.Should().Be(valorA);

        var vistaB = catalogo.Should().ContainSingle(c => c.Id == categoriaB).Subject;
        vistaB.Categoria.Should().Be(categoriaB);
        vistaB.Valores.Should().ContainSingle(v => v.ValorNormalizado == valorB)
            .Which.Valor.Should().Be(valorB);
    }

    // CA-2: asignaciones repetidas del MISMO par (categoria, valor), en streams (colaboradores)
    // distintos, no duplican ni la categoria ni el valor -- el checkpoint (categoria centinela,
    // ver el comentario del encabezado) garantiza que ambas asignaciones ya fueron aplicadas antes
    // de aseverar Count == 1.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_NoDuplicaCategoriaNiValor_CuandoSeAsignaElMismoParDosVeces()
    {
        var ct = TestContext.Current.CancellationToken;
        var categoria = $"duplicado357-{NuevoSufijoUnico()}";
        var valor = "mismovalor";
        var categoriaCentinela = $"centinela357-{NuevoSufijoUnico()}";

        var numero1 = NuevoNumeroIdentificacion();
        var numero2 = NuevoNumeroIdentificacion();
        var numeroCentinela = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numero1, new DateOnly(2026, 1, 1), ct);
        await RegistrarColaboradorAsync(numero2, new DateOnly(2026, 1, 1), ct);
        await RegistrarColaboradorAsync(numeroCentinela, new DateOnly(2026, 1, 1), ct);

        await AsignarEtiquetaAsync(numero1, categoria, valor, ct);
        await AsignarEtiquetaAsync(numero2, categoria, valor, ct); // mismo par, stream distinto
        await AsignarEtiquetaAsync(numeroCentinela, categoriaCentinela, "checkpoint", ct);

        // El checkpoint (orden global del daemon Async, ver el comentario del encabezado) garantiza
        // que las DOS asignaciones anteriores ya se aplicaron cuando la categoria centinela aparece.
        var catalogo = await EsperarCatalogoAsync(lista => lista.Any(c => c.Id == categoriaCentinela), ct);

        var vista = catalogo.Should().ContainSingle(c => c.Id == categoria).Subject;
        vista.Valores.Should().ContainSingle(v => v.ValorNormalizado == valor,
            "el mismo par (categoria, valor) asignado dos veces no deberia duplicar ni la categoria ni el valor");
    }

    // CA-3: dos formas originales que normalizan igual (mayusculas vs minusculas) colapsan en UNA
    // sola categoria -- el display de la categoria y el display del valor reflejan la ULTIMA
    // asignacion que los toco (mismo espiritu que la sobrescritura de FichaColaborador, #356).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_ColapsaEnUnaSolaCategoriaConDisplayDeLaUltima_CuandoDosFormasNormalizanIgual()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = NuevoSufijoUnico();
        var categoriaMayusculas = $"COLAPSO357-{sufijo}";
        var categoriaMinusculas = categoriaMayusculas.ToLowerInvariant(); // misma categoria normalizada
        var categoriaCentinela = $"centinela357-{NuevoSufijoUnico()}";

        var numero1 = NuevoNumeroIdentificacion();
        var numero2 = NuevoNumeroIdentificacion();
        var numeroCentinela = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numero1, new DateOnly(2026, 1, 1), ct);
        await RegistrarColaboradorAsync(numero2, new DateOnly(2026, 1, 1), ct);
        await RegistrarColaboradorAsync(numeroCentinela, new DateOnly(2026, 1, 1), ct);

        await AsignarEtiquetaAsync(numero1, categoriaMayusculas, "PRIMERVALOR", ct);
        // Misma etiqueta por valor (categoria y valor normalizan igual), forma display distinta:
        // "la ultima gana" debe reflejarse tanto en Categoria como en el Valor del ValorCategoria.
        await AsignarEtiquetaAsync(numero2, categoriaMinusculas, "primervalor", ct);
        await AsignarEtiquetaAsync(numeroCentinela, categoriaCentinela, "checkpoint", ct);

        var catalogo = await EsperarCatalogoAsync(lista => lista.Any(c => c.Id == categoriaCentinela), ct);

        var vista = catalogo.Should().ContainSingle(c => c.Id == categoriaMinusculas,
            "dos formas originales que normalizan igual deberian colapsar en UNA sola categoria").Subject;

        vista.Categoria.Should().Be(categoriaMinusculas,
            "el display de la categoria deberia reflejar la ULTIMA asignacion que la toco");
        vista.Valores.Should().ContainSingle(
                "el mismo valor por forma distinta tambien deberia colapsar (upsert por ValorNormalizado)")
            .Which.Valor.Should().Be("primervalor",
                "el display del valor tambien refleja la ultima asignacion");
    }

    // CA-4: sobrescribir el valor de una categoria existente (mismo colaborador, mismo stream)
    // AGREGA el valor nuevo al catalogo y CONSERVA el anterior -- catalogo acumulativo (decision de
    // refinamiento 2026-08-13), nunca un reemplazo total de la lista de valores.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_AgregaElValorNuevoYConservaElAnterior_CuandoSeSobrescribeUnaCategoriaExistente()
    {
        var ct = TestContext.Current.CancellationToken;
        var categoria = $"acumulativo357-{NuevoSufijoUnico()}";
        var valorAnterior = "valoranterior";
        var valorNuevo = "valornuevo";

        var numero = NuevoNumeroIdentificacion();
        await RegistrarColaboradorAsync(numero, new DateOnly(2026, 1, 1), ct);

        await AsignarEtiquetaAsync(numero, categoria, valorAnterior, ct);
        await AsignarEtiquetaAsync(numero, categoria, valorNuevo, ct); // sobrescribe la categoria

        var catalogo = await EsperarCatalogoAsync(
            lista => lista.Any(c => c.Id == categoria
                && c.Valores.Any(v => v.ValorNormalizado == valorAnterior)
                && c.Valores.Any(v => v.ValorNormalizado == valorNuevo)),
            ct);

        var vista = catalogo.Should().ContainSingle(c => c.Id == categoria).Subject;
        vista.Valores.Should().Contain(v => v.ValorNormalizado == valorAnterior,
            "el catalogo es ACUMULATIVO: sobrescribir no deberia borrar el valor anterior");
        vista.Valores.Should().Contain(v => v.ValorNormalizado == valorNuevo,
            "sobrescribir deberia agregar el valor nuevo al catalogo");
    }

    // CA-5: EtiquetaRetirada NO altera el catalogo -- la proyeccion no declara ningun metodo para
    // ese evento (garantia estructural). El checkpoint (categoria centinela asignada DESPUES del
    // retiro) garantiza que el retiro ya fue considerado por el daemon antes de aseverar que el
    // catalogo no cambio.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCategoriasDeEtiquetas_ConservaLaCategoriaYElValor_CuandoSeRetiraLaEtiqueta()
    {
        var ct = TestContext.Current.CancellationToken;
        var categoria = $"retiro357-{NuevoSufijoUnico()}";
        var valor = "valorretirado";
        var categoriaCentinela = $"centinela357-{NuevoSufijoUnico()}";

        var numero = NuevoNumeroIdentificacion();
        var numeroCentinela = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numero, new DateOnly(2026, 1, 1), ct);
        await RegistrarColaboradorAsync(numeroCentinela, new DateOnly(2026, 1, 1), ct);

        await AsignarEtiquetaAsync(numero, categoria, valor, ct);

        // Esperar a que la categoria aparezca ANTES de retirarla -- confirma que el arrange base ya
        // esta materializado (mismo criterio que ObtenerFichaColaboradorSmokeTests: no encadenar mas
        // eventos sobre una vista que todavia no reflejo el primero).
        await EsperarCatalogoAsync(lista => lista.Any(c => c.Id == categoria), ct);

        await RetirarEtiquetaAsync(numero, categoria, ct);
        await AsignarEtiquetaAsync(numeroCentinela, categoriaCentinela, "checkpoint", ct);

        // El checkpoint garantiza que el retiro (emitido antes) ya fue procesado por el daemon --
        // CategoriaDeEtiquetasProjection no declara Apply para EtiquetaRetirada, asi que el catalogo
        // NO deberia cambiar.
        var catalogo = await EsperarCatalogoAsync(lista => lista.Any(c => c.Id == categoriaCentinela), ct);

        var vista = catalogo.Should().ContainSingle(c => c.Id == categoria,
            "EtiquetaRetirada no deberia alterar el catalogo (acumulativo, CA-5)").Subject;
        vista.Valores.Should().ContainSingle(v => v.ValorNormalizado == valor);
    }
}
