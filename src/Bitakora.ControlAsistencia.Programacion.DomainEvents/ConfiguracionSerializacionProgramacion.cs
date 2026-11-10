using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Vive en el ensamblado de los tipos ricos para que cualquier proceso que lea estos streams
// (write-side o read-side) invoque la misma lista.
public static class ConfiguracionSerializacionProgramacion
{
    public static JsonSerializerOptions CrearOpcionesMarten()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        ConfigurarResolver(resolver);
        return new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            PropertyNamingPolicy = null
        };
    }

    // Tipos con ctor privado y private set, que STJ solo reconstruye con el resolver custom
    // (MEF-ADR-0012). Van de mas interno a mas externo: SubFranja es hija de FranjaOrdinaria y esta es
    // payload de TurnoCreado. ProgramacionTurnoSolicitada no entra: su ctor publico es el unico.
    public static void ConfigurarResolver(DefaultJsonTypeInfoResolver resolver)
    {
        SubFranja.ConfigurarSerializacion(resolver);
        FranjaOrdinaria.ConfigurarSerializacion(resolver);
        MotivoAusencia.ConfigurarSerializacion(resolver);
        ModalidadJornada.ConfigurarSerializacion(resolver);
        ModalidadDeJornadaAsignada.ConfigurarSerializacion(resolver);
        ModalidadDeJornadaQuitada.ConfigurarSerializacion(resolver);
        DescansoAcumuladoDeJornadaMarcado.ConfigurarSerializacion(resolver);
        DescansoAcumuladoDeJornadaDesmarcado.ConfigurarSerializacion(resolver);
        Turno.ConfigurarSerializacion(resolver);
        TurnoCreado.ConfigurarSerializacion(resolver);
        TurnoRetirado.ConfigurarSerializacion(resolver);
        FranjaAgregada.ConfigurarSerializacion(resolver);
        DescansoAgregado.ConfigurarSerializacion(resolver);
        ExtraAgregado.ConfigurarSerializacion(resolver);
        FranjaQuitada.ConfigurarSerializacion(resolver);
        DescansoQuitado.ConfigurarSerializacion(resolver);
        ExtraQuitado.ConfigurarSerializacion(resolver);
        SedeDeFranjaAsignada.ConfigurarSerializacion(resolver);
        SedeDeFranjaRetirada.ConfigurarSerializacion(resolver);
        PlantillaDeTurnosCreada.ConfigurarSerializacion(resolver);
        DiaDePlantillaDeTurnosAsignado.ConfigurarSerializacion(resolver);
        PlantillaDeTurnosRetirada.ConfigurarSerializacion(resolver);
        JornadaDePlantillaDeTurnosAsignada.ConfigurarSerializacion(resolver);
        JornadaDePlantillaDeTurnosQuitada.ConfigurarSerializacion(resolver);
        HorasYMinutos.ConfigurarSerializacion(resolver);
        LimitesJornada.ConfigurarSerializacion(resolver);
        JornadaCreada.ConfigurarSerializacion(resolver);
        LimitesJornadaModificados.ConfigurarSerializacion(resolver);
        VentanaDeEntradaModificada.ConfigurarSerializacion(resolver);
        AdvertenciaPlantillaTurnos.ConfigurarSerializacion(resolver);
        AdvertenciasDePlantillaDeTurnosCalculadas.ConfigurarSerializacion(resolver);
        TurnoDePlantillaDeTurnosSincronizado.ConfigurarSerializacion(resolver);
        LimitesDeJornadaDePlantillaDeTurnosSincronizados.ConfigurarSerializacion(resolver);
    }
}
