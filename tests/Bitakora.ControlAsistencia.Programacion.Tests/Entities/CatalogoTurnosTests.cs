// Issue #288 CA-3: coherencia entre CatalogoTurnos.ObtenerDetalle().Descripcion y
// CatalogoTurnos.ToString(). El formato de nivel turno ("{nombre} {franjas}") vive en este
// aggregate, en el Function App de Programacion (el worker de proyecciones no puede referenciarlo,
// MEF-ADR-0034 seccion 5). ObtenerDetalle() e Iniciar() son internal (ADR-0015); accesibles en este
// proyecto de tests via InternalsVisibleTo (Bitakora.ControlAsistencia.Programacion.csproj).
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class CatalogoTurnosTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000099");

    private static CatalogoTurnos CrearCatalogo(params DatosFranja[] franjas) =>
        CatalogoTurnos.Iniciar(TurnoCreado.Crear(TurnoId, "Turno Manana", franjas));

    private static DatosFranja Ordinaria(
        TimeOnly inicio, TimeOnly fin, List<(TimeOnly inicio, TimeOnly fin)>? descansos = null) =>
        new(inicio, fin, descansos ?? [], []);

    [Fact]
    public void ObtenerDetalle_TieneDescripcionCoherenteConToString_CuandoTurnoConUnaOrdinaria()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));

        var detalle = catalogo.ObtenerDetalle();

        detalle.Descripcion.Should().Be(catalogo.ToString());
    }

    // El ToString() del turno concatena las ordinarias y arrastra los labels .resx de los
    // descansos (MEF-ADR-0009): el caso compuesto es el que puede divergir, no el simple.
    [Fact]
    public void ObtenerDetalle_TieneDescripcionCoherenteConToString_CuandoTurnoPartidoConDescanso()
    {
        var catalogo = CrearCatalogo(
            Ordinaria(new TimeOnly(6, 0), new TimeOnly(12, 0),
                [(new TimeOnly(9, 0), new TimeOnly(9, 15))]),
            Ordinaria(new TimeOnly(14, 0), new TimeOnly(18, 0)));

        var detalle = catalogo.ObtenerDetalle();

        detalle.Descripcion.Should().Be(catalogo.ToString());
        detalle.FranjasOrdinarias.Should().HaveCount(2);
        detalle.FranjasOrdinarias[0].Descansos[0].Descripcion.Should().Be("(09:00-09:15)");
    }

    // ---------- Issue #335 CA-1/CA-2: sede prearmada por franja en el catalogo ----------

    // CA-1: turno partido con sede diferente en cada franja (ej. "Vigilante partido": manana ->
    // Suba, tarde -> Chapinero) -- el detalle del catalogo expone la sede de cada franja.
    [Fact]
    public void ObtenerDetalle_ExponeSedePorFranja_CuandoTurnoPrearmaSedesDiferentesEnCadaFranja()
    {
        var sedeManana = new SedeProgramada("SEDE-SUBA", "Suba");
        var sedeTarde = new SedeProgramada("SEDE-CHAPINERO", "Chapinero");
        var catalogo = CrearCatalogo(
            new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [], sedeManana),
            new DatosFranja(new TimeOnly(14, 0), new TimeOnly(22, 0), [], [], sedeTarde));

        var detalle = catalogo.ObtenerDetalle();

        detalle.FranjasOrdinarias[0].Sede.Should().Be(sedeManana);
        detalle.FranjasOrdinarias[1].Sede.Should().Be(sedeTarde);
    }

    // CA-2: regresion -- un turno sin sedes prearmadas conserva el comportamiento actual.
    [Fact]
    public void ObtenerDetalle_DejaSedeNull_CuandoTurnoNoPrearmaNingunaSede()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));

        var detalle = catalogo.ObtenerDetalle();

        detalle.FranjasOrdinarias[0].Sede.Should().BeNull();
    }

    // ---------- CA-4: EstaCompleto() y las tres formas de ToString() ----------

    private static CatalogoTurnos CrearCatalogoDescanso(string nombre) =>
        CatalogoTurnos.Iniciar(TurnoCreado.CrearDescanso(TurnoId, nombre));

    [Fact]
    public void EstaCompleto_EsTrue_CuandoTurnoEsDescanso()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");

        catalogo.EstaCompleto().Should().BeTrue();
        catalogo.ToString().Should().Be($"Descanso Compensatorio {CatalogoTurnos.Mensajes.LabelDescanso}");
    }

    [Fact]
    public void EstaCompleto_EsFalse_CuandoTurnoNaceVacioSinMarcaDeDescanso()
    {
        var catalogo = CrearCatalogo();

        catalogo.EstaCompleto().Should().BeFalse();
        catalogo.ToString().Should().Be($"Turno Manana {CatalogoTurnos.Mensajes.LabelIncompleto}");
    }

    [Fact]
    public void EstaCompleto_EsTrue_CuandoTurnoTieneAlMenosUnaFranja()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));

        catalogo.EstaCompleto().Should().BeTrue();
        catalogo.ToString().Should().Be("Turno Manana (06:00-14:00)");
    }

    // ---------- Issue #613 CA-4: EvaluarAsignabilidad() y su precedencia ----------

    [Fact]
    public void EvaluarAsignabilidad_EsRetirado_CuandoElTurnoFueRetirado()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));
        catalogo.Retirar();

        catalogo.EvaluarAsignabilidad().Should().Be(ResultadoAsignabilidadTurno.Retirado);
    }

    [Fact]
    public void EvaluarAsignabilidad_EsIncompleto_CuandoElTurnoNaceVacioSinMarcaDeDescanso()
    {
        var catalogo = CrearCatalogo();

        catalogo.EvaluarAsignabilidad().Should().Be(ResultadoAsignabilidadTurno.Incompleto);
    }

    [Fact]
    public void EvaluarAsignabilidad_EsAsignable_CuandoElTurnoTieneAlMenosUnaFranja()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));

        catalogo.EvaluarAsignabilidad().Should().Be(ResultadoAsignabilidadTurno.Asignable);
    }

    [Fact]
    public void EvaluarAsignabilidad_EsAsignable_CuandoElTurnoEsDescanso()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");

        catalogo.EvaluarAsignabilidad().Should().Be(ResultadoAsignabilidadTurno.Asignable);
    }

    // Precedencia (CA-4): un turno retirado con cero franjas y sin marca de descanso -- por
    // completitud seria Incompleto, pero Retirado gana la precedencia.
    [Fact]
    public void EvaluarAsignabilidad_EsRetirado_CuandoElTurnoEstaRetiradoYAdemasIncompleto()
    {
        var catalogo = CrearCatalogo();
        catalogo.Retirar();

        catalogo.EvaluarAsignabilidad().Should().Be(ResultadoAsignabilidadTurno.Retirado);
    }

    // ---------- Issue #602 CA-2/CA-3/CA-4: AgregarFranja y su precedencia ----------

    // CA-2: el turno pasa de incompleto a completo con la primera franja.
    [Fact]
    public void AgregarFranja_RetornaAgregada_CuandoElTurnoEstaIncompleto()
    {
        var catalogo = CrearCatalogo();
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0));

        var resultado = catalogo.AgregarFranja(franja);

        resultado.Should().Be(ResultadoAgregarFranja.Agregada);
        catalogo.UncommittedEvents.OfType<FranjaAgregada>().Should().ContainSingle()
            .Which.Franja.Should().Be(franja);
        catalogo.EstaCompleto().Should().BeTrue();
        catalogo.ToString().Should().Be("Turno Manana (22:00-06:00+1)");
    }

    // CA-3: fin exclusivo -- una franja contigua a la existente no se solapa y se agrega,
    // conservando el orden de insercion en ToString().
    [Fact]
    public void AgregarFranja_RetornaAgregada_CuandoLaNuevaFranjaEsContiguaALaExistente()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));
        var nueva = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));

        var resultado = catalogo.AgregarFranja(nueva);

        resultado.Should().Be(ResultadoAgregarFranja.Agregada);
        catalogo.ToString().Should().Be("Turno Manana (06:00-14:00)(14:00-22:00)");
    }

    // CA-3: una franja que se superpone parcialmente con la existente se rechaza sin emitir evento.
    [Fact]
    public void AgregarFranja_RetornaSeSolapaConOtraFranja_CuandoLaNuevaFranjaSeSuperponeConLaExistente()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));
        var nueva = FranjaOrdinaria.Crear(new TimeOnly(10, 0), new TimeOnly(12, 0));

        var resultado = catalogo.AgregarFranja(nueva);

        resultado.Should().Be(ResultadoAgregarFranja.SeSolapaConOtraFranja);
        catalogo.UncommittedEvents.OfType<FranjaAgregada>().Should().BeEmpty();
        catalogo.ObtenerDetalle().FranjasOrdinarias.Should().HaveCount(1);
    }

    // CA-4: un turno de descanso no admite franjas ordinarias.
    [Fact]
    public void AgregarFranja_RetornaTurnoEsDescanso_CuandoElTurnoEsDeDescanso()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));

        var resultado = catalogo.AgregarFranja(franja);

        resultado.Should().Be(ResultadoAgregarFranja.TurnoEsDescanso);
        catalogo.UncommittedEvents.OfType<FranjaAgregada>().Should().BeEmpty();
    }

    // CA-4: un turno retirado no admite nuevas franjas.
    [Fact]
    public void AgregarFranja_RetornaTurnoRetirado_CuandoElTurnoFueRetirado()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));
        catalogo.Retirar();
        var franja = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));

        var resultado = catalogo.AgregarFranja(franja);

        resultado.Should().Be(ResultadoAgregarFranja.TurnoRetirado);
        catalogo.UncommittedEvents.OfType<FranjaAgregada>().Should().BeEmpty();
    }

    // CA-4: precedencia -- un turno de descanso retirado devuelve TurnoRetirado, no TurnoEsDescanso.
    [Fact]
    public void AgregarFranja_RetornaTurnoRetirado_CuandoElTurnoEsDescansoYAdemasFueRetirado()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");
        catalogo.Retirar();
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));

        var resultado = catalogo.AgregarFranja(franja);

        resultado.Should().Be(ResultadoAgregarFranja.TurnoRetirado);
    }

    // ---------- Issue #603: AgregarDescanso/AgregarExtra sobre una franja existente ----------

    // CA-2: camino feliz -- localiza la franja por hora de inicio, delega en ConDescanso y emite.
    [Fact]
    public void AgregarDescanso_RetornaAgregada_CuandoLaFranjaExisteYElDescansoEsValido()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(22, 0), new TimeOnly(6, 0)));

        var resultado = catalogo.AgregarDescanso(
            new TimeOnly(22, 0), new TimeOnly(2, 0), new TimeOnly(2, 30));

        resultado.Should().Be(ResultadoAgregarSubFranja.Agregada);
        catalogo.UncommittedEvents.OfType<DescansoAgregado>().Should().ContainSingle()
            .Which.Franja.ToString().Should().Be("(22:00-06:00+1)[Descansos:(02:00+1-02:30+1)]");
        catalogo.ToString().Should().Be("Turno Manana (22:00-06:00+1)[Descansos:(02:00+1-02:30+1)]");
    }

    // CA-2: AgregarExtra sobre la franja resultante conserva el descanso previo (inmutabilidad del VO).
    [Fact]
    public void AgregarExtra_ConservaElDescansoPrevio_CuandoSeAgregaSobreUnaFranjaConDescansoYaAgregado()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(22, 0), new TimeOnly(6, 0)));
        catalogo.AgregarDescanso(new TimeOnly(22, 0), new TimeOnly(2, 0), new TimeOnly(2, 30));

        var resultado = catalogo.AgregarExtra(
            new TimeOnly(22, 0), new TimeOnly(5, 0), new TimeOnly(6, 0));

        resultado.Should().Be(ResultadoAgregarSubFranja.Agregada);
        catalogo.UncommittedEvents.OfType<ExtraAgregado>().Should().ContainSingle()
            .Which.Franja.ToString().Should().Be(
                "(22:00-06:00+1)[Descansos:(02:00+1-02:30+1)][Extras:(05:00+1-06:00+1)]");
        catalogo.ToString().Should().Be(
            "Turno Manana (22:00-06:00+1)[Descansos:(02:00+1-02:30+1)][Extras:(05:00+1-06:00+1)]");
    }

    // CA-3: ninguna franja empieza a esa hora.
    [Fact]
    public void AgregarDescanso_RetornaFranjaNoExiste_CuandoNingunaFranjaEmpiezaAEsaHora()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(22, 0), new TimeOnly(6, 0)));

        var resultado = catalogo.AgregarDescanso(
            new TimeOnly(23, 0), new TimeOnly(2, 0), new TimeOnly(2, 30));

        resultado.Should().Be(ResultadoAgregarSubFranja.FranjaNoExiste);
        catalogo.UncommittedEvents.OfType<DescansoAgregado>().Should().BeEmpty();
    }

    // CA-3: un turno de descanso no admite sub-franjas.
    [Fact]
    public void AgregarDescanso_RetornaTurnoEsDescanso_CuandoElTurnoEsDeDescanso()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");

        var resultado = catalogo.AgregarDescanso(
            new TimeOnly(22, 0), new TimeOnly(2, 0), new TimeOnly(2, 30));

        resultado.Should().Be(ResultadoAgregarSubFranja.TurnoEsDescanso);
        catalogo.UncommittedEvents.OfType<DescansoAgregado>().Should().BeEmpty();
    }

    // CA-3: un turno retirado no admite nuevas sub-franjas.
    [Fact]
    public void AgregarDescanso_RetornaTurnoRetirado_CuandoElTurnoFueRetirado()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(22, 0), new TimeOnly(6, 0)));
        catalogo.Retirar();

        var resultado = catalogo.AgregarDescanso(
            new TimeOnly(22, 0), new TimeOnly(2, 0), new TimeOnly(2, 30));

        resultado.Should().Be(ResultadoAgregarSubFranja.TurnoRetirado);
        catalogo.UncommittedEvents.OfType<DescansoAgregado>().Should().BeEmpty();
    }

    // CA-3: precedencia -- retirado gana sobre descanso.
    [Fact]
    public void AgregarDescanso_RetornaTurnoRetirado_CuandoElTurnoEsDescansoYAdemasFueRetirado()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");
        catalogo.Retirar();

        var resultado = catalogo.AgregarDescanso(
            new TimeOnly(22, 0), new TimeOnly(2, 0), new TimeOnly(2, 30));

        resultado.Should().Be(ResultadoAgregarSubFranja.TurnoRetirado);
    }

    // CA-4: invariante estructural del VO (hija fuera del contenedor) sube sin capturarse -- la
    // franja original queda intacta (inmutabilidad de FranjaOrdinaria.ConDescanso).
    [Fact]
    public void AgregarDescanso_DejaSubirArgumentException_CuandoLaHijaQuedaFueraDeLaFranjaContenedora()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(22, 0), new TimeOnly(6, 0)));

        var act = () => catalogo.AgregarDescanso(
            new TimeOnly(22, 0), new TimeOnly(5, 0), new TimeOnly(7, 0));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor}*");
        catalogo.ToString().Should().Be("Turno Manana (22:00-06:00+1)");
    }

    // MEF-ADR-0004 capa 4: un Apply que lanza deja el aggregate roto para siempre -- si el stream
    // trae un DescansoAgregado cuya franja contenedora ya no esta (anomalia, o retirada por un
    // evento posterior de #605), la rehidratacion lo ignora en vez de indexar con -1.
    [Fact]
    public void Apply_NoLanzaYDejaLasFranjasIntactas_CuandoNingunaEmpiezaALaHoraDelEvento()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(22, 0), new TimeOnly(6, 0)));
        var franjaHuerfana = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))
            .ConDescanso(new TimeOnly(9, 0), new TimeOnly(9, 15));

        var act = () => catalogo.Apply(DescansoAgregado.Crear(TurnoId, franjaHuerfana));

        act.Should().NotThrow();
        catalogo.ToString().Should().Be("Turno Manana (22:00-06:00+1)");
    }

    // CA-4: solape con una hermana ya presente.
    [Fact]
    public void AgregarDescanso_DejaSubirArgumentException_CuandoSeSuperponeConUnaHermana()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(22, 0), new TimeOnly(6, 0)));
        catalogo.AgregarDescanso(new TimeOnly(22, 0), new TimeOnly(2, 0), new TimeOnly(2, 30));

        var act = () => catalogo.AgregarDescanso(
            new TimeOnly(22, 0), new TimeOnly(2, 15), new TimeOnly(2, 45));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjasHijasSeSuperponen}*");
        catalogo.UncommittedEvents.OfType<DescansoAgregado>().Should().ContainSingle(
            "el descanso rechazado no emite un segundo evento");
    }

    // ---------- QuitarFranja y su precedencia ----------

    [Fact]
    public void QuitarFranja_RetornaQuitada_CuandoLaFranjaExisteYQuedanOtras()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");
        var catalogo = CrearCatalogo(
            new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0),
                [(new TimeOnly(9, 0), new TimeOnly(9, 15))], [], sede),
            Ordinaria(new TimeOnly(14, 0), new TimeOnly(22, 0)));

        var resultado = catalogo.QuitarFranja(new TimeOnly(6, 0));

        resultado.Should().Be(ResultadoQuitarFranja.Quitada);
        catalogo.UncommittedEvents.OfType<FranjaQuitada>().Should().ContainSingle()
            .Which.Franja.ToString().Should().Be(
                "(06:00-14:00)[Descansos:(09:00-09:15)][sede:Suba]");
        catalogo.ToString().Should().Be("Turno Manana (14:00-22:00)");
        catalogo.EstaCompleto().Should().BeTrue();
    }

    // Incompleto y descanso son dos ToString() distintos: quitar la ultima franja da el primero.
    [Fact]
    public void QuitarFranja_RetornaQuitada_CuandoEraLaUnicaFranja()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));

        var resultado = catalogo.QuitarFranja(new TimeOnly(6, 0));

        resultado.Should().Be(ResultadoQuitarFranja.Quitada);
        catalogo.EstaCompleto().Should().BeFalse();
        catalogo.ToString().Should().Be($"Turno Manana {CatalogoTurnos.Mensajes.LabelIncompleto}");
    }

    [Fact]
    public void QuitarFranja_RetornaFranjaNoExiste_CuandoNingunaFranjaEmpiezaAEsaHora()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));

        var resultado = catalogo.QuitarFranja(new TimeOnly(7, 0));

        resultado.Should().Be(ResultadoQuitarFranja.FranjaNoExiste);
        catalogo.UncommittedEvents.OfType<FranjaQuitada>().Should().BeEmpty();
        catalogo.ObtenerDetalle().FranjasOrdinarias.Should().HaveCount(1);
    }

    // Un descanso no tiene franjas ordinarias: cae en FranjaNoExiste, sin resultado propio.
    [Fact]
    public void QuitarFranja_RetornaFranjaNoExiste_CuandoElTurnoEsDescanso()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");

        var resultado = catalogo.QuitarFranja(new TimeOnly(6, 0));

        resultado.Should().Be(ResultadoQuitarFranja.FranjaNoExiste);
        catalogo.UncommittedEvents.OfType<FranjaQuitada>().Should().BeEmpty();
    }

    [Fact]
    public void QuitarFranja_RetornaTurnoRetirado_CuandoElTurnoFueRetirado()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));
        catalogo.Retirar();

        var resultado = catalogo.QuitarFranja(new TimeOnly(6, 0));

        resultado.Should().Be(ResultadoQuitarFranja.TurnoRetirado);
        catalogo.UncommittedEvents.OfType<FranjaQuitada>().Should().BeEmpty();
    }

    // Retirado gana incluso sobre un descanso, que por si solo daria FranjaNoExiste.
    [Fact]
    public void QuitarFranja_RetornaTurnoRetirado_CuandoElTurnoEsDescansoYAdemasFueRetirado()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");
        catalogo.Retirar();

        var resultado = catalogo.QuitarFranja(new TimeOnly(6, 0));

        resultado.Should().Be(ResultadoQuitarFranja.TurnoRetirado);
    }

    // ---------- QuitarDescanso/QuitarExtra y su precedencia ----------

    private static CatalogoTurnos CrearCatalogoConFranjaYDosDescansos()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(22, 0), new TimeOnly(6, 0)));
        catalogo.AgregarDescanso(new TimeOnly(22, 0), new TimeOnly(23, 0), new TimeOnly(23, 30));
        catalogo.AgregarDescanso(new TimeOnly(22, 0), new TimeOnly(2, 0), new TimeOnly(2, 30));
        return catalogo;
    }

    [Fact]
    public void QuitarDescanso_RetornaQuitada_CuandoElDescansoExisteYQuedaOtro()
    {
        var catalogo = CrearCatalogoConFranjaYDosDescansos();

        var resultado = catalogo.QuitarDescanso(new TimeOnly(22, 0), new TimeOnly(23, 0));

        resultado.Should().Be(ResultadoQuitarSubFranja.Quitada);
        catalogo.UncommittedEvents.OfType<DescansoQuitado>().Should().ContainSingle()
            .Which.Franja.ToString().Should().Be("(22:00-06:00+1)[Descansos:(02:00+1-02:30+1)]");
        catalogo.ToString().Should().Be(
            "Turno Manana (22:00-06:00+1)[Descansos:(02:00+1-02:30+1)]");
    }

    // La hija a esa hora existe, pero es del otro tipo.
    [Fact]
    public void QuitarExtra_RetornaSubFranjaNoExiste_CuandoLaHijaAEsaHoraEsUnDescanso()
    {
        var catalogo = CrearCatalogoConFranjaYDosDescansos();

        var resultado = catalogo.QuitarExtra(new TimeOnly(22, 0), new TimeOnly(23, 0));

        resultado.Should().Be(ResultadoQuitarSubFranja.SubFranjaNoExiste);
        catalogo.UncommittedEvents.OfType<ExtraQuitado>().Should().BeEmpty();
    }

    [Fact]
    public void QuitarDescanso_RetornaFranjaNoExiste_CuandoNingunaFranjaEmpiezaAEsaHora()
    {
        var catalogo = CrearCatalogoConFranjaYDosDescansos();

        var resultado = catalogo.QuitarDescanso(new TimeOnly(23, 0), new TimeOnly(23, 0));

        resultado.Should().Be(ResultadoQuitarSubFranja.FranjaNoExiste);
        catalogo.UncommittedEvents.OfType<DescansoQuitado>().Should().BeEmpty();
    }

    // Un descanso no tiene franjas ordinarias: cae en FranjaNoExiste, sin resultado propio.
    [Fact]
    public void QuitarDescanso_RetornaFranjaNoExiste_CuandoElTurnoEsDescanso()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");

        var resultado = catalogo.QuitarDescanso(new TimeOnly(22, 0), new TimeOnly(23, 0));

        resultado.Should().Be(ResultadoQuitarSubFranja.FranjaNoExiste);
        catalogo.UncommittedEvents.OfType<DescansoQuitado>().Should().BeEmpty();
    }

    [Fact]
    public void QuitarDescanso_RetornaTurnoRetirado_CuandoElTurnoFueRetirado()
    {
        var catalogo = CrearCatalogoConFranjaYDosDescansos();
        catalogo.Retirar();

        var resultado = catalogo.QuitarDescanso(new TimeOnly(22, 0), new TimeOnly(23, 0));

        resultado.Should().Be(ResultadoQuitarSubFranja.TurnoRetirado);
        catalogo.UncommittedEvents.OfType<DescansoQuitado>().Should().BeEmpty();
    }

    // Retirado gana incluso sobre un descanso, que por si solo daria FranjaNoExiste.
    [Fact]
    public void QuitarDescanso_RetornaTurnoRetirado_CuandoElTurnoEsDescansoYAdemasFueRetirado()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");
        catalogo.Retirar();

        var resultado = catalogo.QuitarDescanso(new TimeOnly(22, 0), new TimeOnly(23, 0));

        resultado.Should().Be(ResultadoQuitarSubFranja.TurnoRetirado);
    }

    // ---------- Issue #606 CA-3: AsignarSedeAFranja asigna, retira, y su precedencia ----------

    private static readonly SedeProgramada Chapinero = new("SEDE-CHAPINERO", "Chapinero");

    [Fact]
    public void AsignarSedeAFranja_RetornaAsignada_CuandoLaFranjaNoTeniaSede()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(14, 0), new TimeOnly(22, 0)));

        var resultado = catalogo.AsignarSedeAFranja(new TimeOnly(14, 0), Chapinero);

        resultado.Should().Be(ResultadoAsignarSedeAFranja.Asignada);
        catalogo.UncommittedEvents.OfType<SedeDeFranjaAsignada>().Should().ContainSingle()
            .Which.Franja.ToDetalle().Sede.Should().Be(Chapinero);
        catalogo.ObtenerDetalle().FranjasOrdinarias[0].Sede.Should().Be(Chapinero);
    }

    [Fact]
    public void AsignarSedeAFranja_RetornaRetirada_CuandoLaSedeEsNullYLaFranjaTeniaSede()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(14, 0), new TimeOnly(22, 0)));
        catalogo.AsignarSedeAFranja(new TimeOnly(14, 0), Chapinero);

        var resultado = catalogo.AsignarSedeAFranja(new TimeOnly(14, 0), null);

        resultado.Should().Be(ResultadoAsignarSedeAFranja.Retirada);
        catalogo.UncommittedEvents.OfType<SedeDeFranjaRetirada>().Should().ContainSingle()
            .Which.Franja.ToDetalle().Sede.Should().BeNull();
        catalogo.ObtenerDetalle().FranjasOrdinarias[0].Sede.Should().BeNull();
    }

    // Nada que retirar: la franja ya no tiene sede -- mismo criterio que
    // ResultadoRetiroTurno.YaEstabaRetirado.
    [Fact]
    public void AsignarSedeAFranja_RetornaFranjaSinSede_CuandoLaFranjaYaNoTieneSedeQueRetirar()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(14, 0), new TimeOnly(22, 0)));

        var resultado = catalogo.AsignarSedeAFranja(new TimeOnly(14, 0), null);

        resultado.Should().Be(ResultadoAsignarSedeAFranja.FranjaSinSede);
        catalogo.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void AsignarSedeAFranja_RetornaFranjaNoExiste_CuandoNingunaFranjaEmpiezaAEsaHora()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(14, 0), new TimeOnly(22, 0)));

        var resultado = catalogo.AsignarSedeAFranja(new TimeOnly(15, 0), Chapinero);

        resultado.Should().Be(ResultadoAsignarSedeAFranja.FranjaNoExiste);
        catalogo.UncommittedEvents.Should().BeEmpty();
    }

    // CA-3: un descanso no tiene franjas ordinarias -- cualquier hora cae en FranjaNoExiste (mismo
    // criterio que QuitarFranja/QuitarDescanso).
    [Fact]
    public void AsignarSedeAFranja_RetornaFranjaNoExiste_CuandoElTurnoEsDescanso()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");

        var resultado = catalogo.AsignarSedeAFranja(new TimeOnly(14, 0), Chapinero);

        resultado.Should().Be(ResultadoAsignarSedeAFranja.FranjaNoExiste);
    }

    [Fact]
    public void AsignarSedeAFranja_RetornaTurnoRetirado_CuandoElTurnoFueRetirado()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(14, 0), new TimeOnly(22, 0)));
        catalogo.Retirar();

        var resultado = catalogo.AsignarSedeAFranja(new TimeOnly(14, 0), Chapinero);

        resultado.Should().Be(ResultadoAsignarSedeAFranja.TurnoRetirado);
        catalogo.UncommittedEvents.OfType<SedeDeFranjaAsignada>().Should().BeEmpty();
    }

    // Precedencia: un descanso retirado devuelve TurnoRetirado, no FranjaNoExiste.
    [Fact]
    public void AsignarSedeAFranja_RetornaTurnoRetirado_CuandoElTurnoEsDescansoYAdemasFueRetirado()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");
        catalogo.Retirar();

        var resultado = catalogo.AsignarSedeAFranja(new TimeOnly(14, 0), Chapinero);

        resultado.Should().Be(ResultadoAsignarSedeAFranja.TurnoRetirado);
    }
}
