using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;

/// <summary>Presentacion del documento de la vista: minutos a { horas, minutos } y descripciones desde .resx.</summary>
public static class PresentadorAdvertencias
{
    public static ElementoAdvertenciasSemana Presentar(AdvertenciasProgramacionSemanal d) =>
        new(d.CodigoColaborador,
            d.NombreCompleto,
            d.Jornada is null ? null : PresentarJornada(d.Jornada),
            TiempoHM.DesdeMinutos(d.MinutosOrdinariosProgramados),
            d.EsJuzgable,
            d.EsJuzgable ? null : MotivoNoJuzgable(d),
            d.DiasSinProgramar,
            d.TieneAusencias,
            d.AdvertenciasSemanales.Select(PresentarSemanal).ToList(),
            d.Casillas.Select(PresentarCasilla).ToList());

    private static JornadaPresentada PresentarJornada(JornadaAplicada j) =>
        new(j.JornadaId,
            Mensajes.Obtener("DescripcionJornada",
                Duracion(j.HorasSemanalesEnMinutos), Duracion(j.TopeDiarioEnMinutos),
                Duracion(j.MinimoDiarioEnMinutos), Dias(j.DiasDescansoPorSemana)),
            TiempoHM.DesdeMinutos(j.HorasSemanalesEnMinutos),
            TiempoHM.DesdeMinutos(j.TopeDiarioEnMinutos),
            TiempoHM.DesdeMinutos(j.MinimoDiarioEnMinutos),
            j.DiasDescansoPorSemana);

    private static CasillaPresentada PresentarCasilla(CasillaDia c) =>
        new(c.Fecha, c.Tipo.ToString(), c.NombreTurno, TiempoHM.DesdeMinutos(c.MinutosOrdinarios),
            c.Advertencias.Select(PresentarDiaria).ToList(), c.MotivoAusencia);

    private static AdvertenciaPresentada PresentarSemanal(AdvertenciaSemanal a)
    {
        var clave = a.Tipo.ToString();
        return a.Tipo switch
        {
            TipoAdvertenciaSemanal.FaltanDiasDeDescanso or TipoAdvertenciaSemanal.SobranDiasDeDescanso =>
                new(clave, null, a.Magnitud, Mensajes.Obtener(clave, a.Magnitud, UnidadDia(a.Magnitud))),
            _ => new(clave, TiempoHM.DesdeMinutos(a.Magnitud), null,
                Mensajes.Obtener(clave, Duracion(a.Magnitud)))
        };
    }

    private static AdvertenciaPresentada PresentarDiaria(AdvertenciaDiaria a) =>
        new(a.Tipo.ToString(), TiempoHM.DesdeMinutos(a.MagnitudEnMinutos), null,
            Mensajes.Obtener(a.Tipo.ToString(), Duracion(a.MagnitudEnMinutos)));

    private static string? MotivoNoJuzgable(AdvertenciasProgramacionSemanal d)
    {
        var motivos = new List<string>();
        if (d.DiasSinProgramar > 0)
            motivos.Add(Mensajes.Obtener("SinProgramar", d.DiasSinProgramar, UnidadDia(d.DiasSinProgramar)));
        if (d.TieneAusencias)
            motivos.Add(Mensajes.Obtener("TieneAusencias"));
        return motivos.Count == 0 ? null : string.Join(Mensajes.Obtener("SeparadorMotivos"), motivos);
    }

    private static string UnidadDia(int n) => Mensajes.Obtener(n == 1 ? "DiaUno" : "DiaVarios");

    private static string Dias(int n) => $"{n} {UnidadDia(n)}";

    private static string Duracion(int minutos)
    {
        var (h, m) = (minutos / 60, minutos % 60);
        return m == 0 ? Mensajes.Obtener("FormatoHoras", h)
            : h == 0 ? Mensajes.Obtener("FormatoMinutos", m)
            : Mensajes.Obtener("FormatoHorasMinutos", h, m);
    }
}
