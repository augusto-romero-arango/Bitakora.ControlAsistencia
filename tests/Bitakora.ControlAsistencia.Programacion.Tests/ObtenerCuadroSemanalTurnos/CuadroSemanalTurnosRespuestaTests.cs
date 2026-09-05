// Issue #625 CA-1..CA-3: unit tests de la funcion PURA de composicion (sin QuerySession, sin
// Postgres) que junta CuadroSemanalTurnos (#624) con las FichaTurno de sus turnos -- opcion B,
// decision del experto 2026-09-05, reemplaza CA-ADR-0034 decision 5. Oraculo independiente
// (MEF-ADR-0002): el esperado se arma a mano con los datos del arrange, nunca ejecutando
// Componer para producirlo.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ObtenerCuadroSemanalTurnos;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerCuadroSemanalTurnos;

public class CuadroSemanalTurnosRespuestaTests
{
    private static readonly string[] TurnoIdsSemana =
        ["turno-1", "turno-2", "turno-3", "turno-4", "turno-5", "turno-6", "turno-7"];

    private static FichaTurno CrearFicha(string id, bool completo) =>
        new(
            Id: id,
            Nombre: $"Turno {id}",
            EsDescanso: false,
            HorarioResumido: "06:00-14:00",
            Franjas: [],
            Descripcion: $"Descripcion {id}",
            Completo: completo);

    // CA-1: cuadro de 1 semana con 7 dias, fichas de todos sus turnos con Completo = true ->
    // Completa = true; cada dia lleva Turno {Id, Nombre, Descripcion, Completo = true,
    // Retirado = false}; Dias conserva el orden (Semana, Dia) de la vista; Id/Nombre/Semanas
    // copiados de la vista.
    [Fact]
    public void Componer_MarcaLaPlantillaCompleta_CuandoLosSieteDiasTienenFichaCompleta()
    {
        var dias = Enumerable.Range(1, 7)
            .Select(dia => new DiaDelCuadro(Semana: 1, Dia: dia, TurnoId: TurnoIdsSemana[dia - 1]))
            .ToArray();
        var cuadro = new CuadroSemanalTurnos("plantilla-001", "Semana Cocina", Semanas: 1, dias);
        var fichasPorId = TurnoIdsSemana.ToDictionary(id => id, id => CrearFicha(id, completo: true));

        var respuesta = CuadroSemanalTurnosRespuesta.Componer(cuadro, fichasPorId);

        respuesta.Id.Should().Be("plantilla-001");
        respuesta.Nombre.Should().Be("Semana Cocina");
        respuesta.Semanas.Should().Be(1);
        respuesta.Completa.Should().BeTrue();
        respuesta.Dias.Should().Equal(dias.Select(d => new DiaDelCuadroRespuesta(
            d.Semana,
            d.Dia,
            new TurnoDelCuadroRespuesta(
                d.TurnoId,
                $"Turno {d.TurnoId}",
                $"Descripcion {d.TurnoId}",
                Completo: true,
                Retirado: false))));
    }

    // CA-2: un TurnoId sin ficha (el turno fue retirado, la proyeccion FichaTurno lo borra en
    // TurnoRetirado) -> ese dia Turno {Retirado = true, Nombre = null, Descripcion = null,
    // Completo = false} y Completa = false; los demas dias intactos.
    [Fact]
    public void Componer_MarcaElDiaRetirado_CuandoElTurnoNoTieneFicha()
    {
        var dias = Enumerable.Range(1, 7)
            .Select(dia => new DiaDelCuadro(1, dia, TurnoIdsSemana[dia - 1]))
            .ToArray();
        var cuadro = new CuadroSemanalTurnos("plantilla-002", "Semana Bodega", 1, dias);
        var turnoIdRetirado = TurnoIdsSemana[2];
        var fichasPorId = TurnoIdsSemana
            .Where(id => id != turnoIdRetirado)
            .ToDictionary(id => id, id => CrearFicha(id, completo: true));

        var respuesta = CuadroSemanalTurnosRespuesta.Componer(cuadro, fichasPorId);

        respuesta.Completa.Should().BeFalse();
        respuesta.Dias[2].Turno.Should().Be(new TurnoDelCuadroRespuesta(
            turnoIdRetirado, Nombre: null, Descripcion: null, Completo: false, Retirado: true));
        respuesta.Dias.Where((_, i) => i != 2).Should().Equal(
            dias.Where(d => d.TurnoId != turnoIdRetirado).Select(d => new DiaDelCuadroRespuesta(
                d.Semana,
                d.Dia,
                new TurnoDelCuadroRespuesta(
                    d.TurnoId,
                    $"Turno {d.TurnoId}",
                    $"Descripcion {d.TurnoId}",
                    Completo: true,
                    Retirado: false))));
    }

    // CA-3 (primer escenario): los 7 x N dias presentes pero una ficha con Completo = false -> ese
    // dia Turno.Completo = false y Completa = false.
    [Fact]
    public void Componer_MarcaLaPlantillaIncompleta_CuandoUnaFichaNoEsCompleta()
    {
        var dias = Enumerable.Range(1, 7)
            .Select(dia => new DiaDelCuadro(1, dia, TurnoIdsSemana[dia - 1]))
            .ToArray();
        var cuadro = new CuadroSemanalTurnos("plantilla-003", "Semana Recepcion", 1, dias);
        var turnoIdIncompleto = TurnoIdsSemana[4];
        var fichasPorId = TurnoIdsSemana.ToDictionary(
            id => id,
            id => CrearFicha(id, completo: id != turnoIdIncompleto));

        var respuesta = CuadroSemanalTurnosRespuesta.Componer(cuadro, fichasPorId);

        respuesta.Completa.Should().BeFalse();
        respuesta.Dias[4].Turno.Should().Be(new TurnoDelCuadroRespuesta(
            turnoIdIncompleto,
            $"Turno {turnoIdIncompleto}",
            $"Descripcion {turnoIdIncompleto}",
            Completo: false,
            Retirado: false));
    }

    // CA-3 (segundo escenario): 6 de 7 dias, todos con ficha completa -> Completa = false (falta
    // un dia por asignar en la semana).
    [Fact]
    public void Componer_MarcaLaPlantillaIncompleta_CuandoFaltaUnDiaDeLosSietePorSemana()
    {
        var dias = Enumerable.Range(1, 6)
            .Select(dia => new DiaDelCuadro(1, dia, TurnoIdsSemana[dia - 1]))
            .ToArray();
        var cuadro = new CuadroSemanalTurnos("plantilla-004", "Semana Turno Parcial", 1, dias);
        var fichasPorId = dias.Select(d => d.TurnoId)
            .ToDictionary(id => id, id => CrearFicha(id, completo: true));

        var respuesta = CuadroSemanalTurnosRespuesta.Componer(cuadro, fichasPorId);

        respuesta.Completa.Should().BeFalse();
        respuesta.Dias.Should().HaveCount(6);
    }

    // CA-1/CA-3 con N > 1: Completa multiplica por Semanas, no compara contra 7 fijo. Sin este par
    // de casos una implementacion que exigiera "7 dias" pasaria toda la bateria de arriba.
    [Fact]
    public void Componer_MarcaLaPlantillaCompleta_CuandoLasDosSemanasTienenSusCatorceDiasConFichaCompleta()
    {
        var dias = DiasDeSemanasCompletas(semanas: 2);
        var cuadro = new CuadroSemanalTurnos("plantilla-005", "Quincena Planta", Semanas: 2, dias);
        var fichasPorId = dias.Select(d => d.TurnoId)
            .ToDictionary(id => id, id => CrearFicha(id, completo: true));

        var respuesta = CuadroSemanalTurnosRespuesta.Componer(cuadro, fichasPorId);

        respuesta.Semanas.Should().Be(2);
        respuesta.Dias.Should().HaveCount(14);
        respuesta.Completa.Should().BeTrue();
    }

    [Fact]
    public void Componer_MarcaLaPlantillaIncompleta_CuandoFaltaLaSegundaSemanaDeDos()
    {
        var dias = DiasDeSemanasCompletas(semanas: 1);
        var cuadro = new CuadroSemanalTurnos("plantilla-006", "Quincena A Medias", Semanas: 2, dias);
        var fichasPorId = dias.Select(d => d.TurnoId)
            .ToDictionary(id => id, id => CrearFicha(id, completo: true));

        var respuesta = CuadroSemanalTurnosRespuesta.Componer(cuadro, fichasPorId);

        respuesta.Dias.Should().HaveCount(7);
        respuesta.Completa.Should().BeFalse();
    }

    private static DiaDelCuadro[] DiasDeSemanasCompletas(int semanas) =>
        [.. Enumerable.Range(1, semanas).SelectMany(semana => Enumerable.Range(1, 7)
            .Select(dia => new DiaDelCuadro(semana, dia, $"turno-s{semana}-d{dia}")))];
}
