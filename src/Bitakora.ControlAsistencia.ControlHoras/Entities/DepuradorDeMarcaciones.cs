using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-122: Clase estatica pura que ejecuta el algoritmo secuencial por rango para asignar
// marcaciones normalizadas a franjas ordinarias del turno.
// Sin estado propio - no necesita inyeccion ni ser instanciada.
// Funciones auxiliares (resolver franjas a DateTime, calcular cortes) son internal/private.
public static class DepuradorDeMarcaciones
{
    public static IReadOnlyList<ControlFranja> Depurar(
        DetalleTurno? turno,
        DateOnly fecha,
        IReadOnlyList<MarcacionNormalizada> marcaciones)
        => throw new NotImplementedException();
}
