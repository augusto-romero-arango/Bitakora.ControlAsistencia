using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #621: pone -- o reemplaza -- el turno de un dia de la plantilla semanal (CA-ADR-0034
// decisiones 1, 3 y 4). Referencia viva al turno (TurnoId), nunca copia: editar el turno despues
// se refleja solo. Mismo patron que PlantillaSemanalCreada: sealed class con ctor privado + ctor
// vacio para Marten/JSON.
public sealed partial class DiaDePlantillaSemanalAsignado
{
    public Guid PlantillaId { get; private set; }
    public int Semana { get; private set; }
    public DiaSemana Dia { get; private set; }
    public Guid TurnoId { get; private set; }

    private DiaDePlantillaSemanalAsignado(Guid plantillaId, int semana, DiaSemana dia, Guid turnoId)
    {
        PlantillaId = plantillaId;
        Semana = semana;
        Dia = dia;
        TurnoId = turnoId;
    }

    // Constructor vacio privado para Marten/JSON (mismo patron que PlantillaSemanalCreada).
    private DiaDePlantillaSemanalAsignado() => Dia = DiaSemana.Lunes;

    // El tope N de semanas es regla del aggregate (PlantillaSemanalTurnos.AsignarDia), no del
    // evento: aqui solo se valida el piso semana >= 1.
    public static DiaDePlantillaSemanalAsignado Crear(
        Guid plantillaId, int semana, DiaSemana dia, Guid turnoId) =>
        throw new NotImplementedException();

    // Dia persiste como su numero ISO (entero), nunca el nombre del enum de .NET ni una etiqueta
    // en espanol -- mismo mecanismo con que Identificacion.ConfigurarSerializacion persiste _tipo
    // como codigo literal y rehidrata via TipoIdentificacion.Desde.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) =>
        throw new NotImplementedException();
}
