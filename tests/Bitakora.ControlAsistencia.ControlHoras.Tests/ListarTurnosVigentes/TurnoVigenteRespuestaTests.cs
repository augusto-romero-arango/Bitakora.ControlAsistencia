using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarTurnosVigentes;

public class TurnoVigenteRespuestaTests
{
    private static readonly DateOnly Fecha = new(2027, 3, 1);

    private static TurnoVigente Documento(bool esHorarioAbierto) =>
        new("cd:A:20270301", "A", "Nombre", Fecha, "Abierto", "04:00-06:00",
            [new Bloque(TipoBloque.Ordinaria, Fecha.ToDateTime(new TimeOnly(4, 0)), Fecha.ToDateTime(new TimeOnly(6, 0)))],
            EsHorarioAbierto: esHorarioAbierto);

    [Fact]
    public void Desde_ExponeLaMarcaDeHorarioAbierto_CuandoElDocumentoLaTrae() =>
        TurnoVigenteRespuesta.Desde(Documento(true)).EsHorarioAbierto.Should().BeTrue();

    [Fact]
    public void Desde_DejaLaMarcaDeHorarioAbiertoEnFalse_CuandoElDocumentoNoLaTrae() =>
        TurnoVigenteRespuesta.Desde(Documento(false)).EsHorarioAbierto.Should().BeFalse();
}
