using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Colaboradores;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using JasperFx.Events; // StreamIdentity, EventNamingStyle (NO Marten.Events, mismo gotcha que DaemonMode)
using JasperFx.Events.Daemon; // DaemonMode (NO Marten.Events.Daemon: compila pero deja DaemonMode sin resolver)
using JasperFx.Events.Projections; // ProjectionLifecycle (NO Marten.Events.Projections)
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*, mismo gotcha que StreamIdentity/DaemonMode)
using Marten;
using Weasel.Core; // EnumStorage, Casing (NO Marten.*: viven en Weasel.Core)
using Weasel.Postgresql.Tables; // IndexMethod (NO Marten.*: vive en Weasel.Postgresql.Tables)

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Marker del named store de proyecciones del dominio Colaboradores (MEF-ADR-0034 seccion 2).
/// </summary>
public interface IColaboradoresProjectionStore : IDocumentStore;

/// <summary>
/// Seam de composicion de proyecciones del dominio Colaboradores (issue #360;
/// MEF-ADR-0006/MEF-ADR-0034 secciones 2 y 6) -- hermano read-side de ComposicionServicios
/// (write-side, MEF-ADR-0029): fuente unica que comparten Program.cs del worker y el config-test.
///
/// Registra el named store sobre la misma conexion y el mismo schema "colaboradores" que ya usa el
/// write-side (ComposicionServicios.AgregarServiciosColaboradores) -- el read-side no crea base ni
/// schema nuevos. Las proyecciones concretas se suman aditivamente dentro de este mismo
/// AddMartenStore; la primera es FichaColaboradorProjection (issue #356).
///
/// El seam se declara con modificadores de acceso y sin partial: un metodo partial sin
/// modificadores desaparece en silencio al compilar si nadie lo implementa, y ademas seria
/// implicitamente privado e inalcanzable desde el ensamblado de tests.
/// </summary>
public static class ConfiguracionMartenProjectionsColaboradores
{
    private const string SchemaDelDominio = "colaboradores";

    public static IServiceCollection ConfigurarColaboradores(
        this IServiceCollection services, string martenConnectionString)
    {
        services.AddMartenStore<IColaboradoresProjectionStore>(opts =>
            {
                opts.Connection(martenConnectionString);
                opts.DatabaseSchemaName = SchemaDelDominio; // mismo schema que el write-side (MEF-ADR-0003)

                // Replica de la identidad de stream que el write-side declara por defecto via
                // Cosmos.EventSourcing.CritterStack (AgregarConfiguracionMartenComandos: Events.
                // StreamIdentity = AsString): el issue #360 describe el stream de
                // ColaboradorAggregateRoot como "stream por Identificacion" -- un valor de texto
                // (cedula), nunca un Guid. Sin esta linea Marten aplica su default AsGuid y el
                // daemon leeria el event store (stream_id varchar) como uuid, sin encontrar ningun
                // stream (mismo sintoma que el issue #253 de ControlHoras/Programacion).
                opts.Events.StreamIdentity = StreamIdentity.AsString;

                // El event store se escribe con tenancy conjoined (AgregarConfiguracionMartenComandos,
                // Cosmos.EventSourcing.CritterStack 2.3.1) y Marten documenta TenancyStyle.Conjoined
                // como un modelo opt-in que el lado que lee debe declarar igual que el que escribio
                // ("Event Store Multi-Tenancy": https://martendb.io/events/multitenancy.html).
                opts.Events.TenancyStyle = TenancyStyle.Conjoined;

                // Replica de Events.EventNamingStyle = SmarterTypeName (idem ControlHoras/Programacion):
                // hoy inocua (eventos top-level, sin anidar), pero sin esta linea un futuro evento
                // anidado calcularia un alias distinto entre write-side y worker, sin ninguna senal
                // en el build.
                opts.Events.EventNamingStyle = EventNamingStyle.SmarterTypeName;

                // Policies.AllDocumentsAreMultiTenanted() gobierna la forma de la tabla de cualquier
                // read model que este worker llegue a materializar para Colaboradores.
                opts.Policies.AllDocumentsAreMultiTenanted();

                // Replica de la configuracion de metadata del write-side (MEF-ADR-0034 seccion 6
                // punto 3, seccion 7): el config-test verifica exactamente estas tres. La
                // habilitacion real de las columnas es responsabilidad del write-side de este
                // dominio (issue #360, ComposicionServicios.AgregarServiciosColaboradores).
                opts.Events.MetadataConfig.CorrelationIdEnabled = true;
                opts.Events.MetadataConfig.CausationIdEnabled = true;
                opts.Events.MetadataConfig.HeadersEnabled = true;

                // Defensa en profundidad read-side: registra los tipos de evento persistidos de
                // Colaboradores en el EventGraph de este named store, para que el daemon no dependa
                // del fallback por mt_dotnet_type al leer streams preexistentes. La lista se lee del
                // mismo artefacto que el write-side (issue #330: ColaboradorRegistrado y
                // VinculacionIniciada), asi que ambos lados no pueden divergir.
                opts.Events.AddEventTypes(IdentidadEventosColaboradores.TiposPersistidos);

                // Issue #330 (par 1 de MEF-ADR-0034 seccion 6, fila "Serializador"): el dominio
                // estrena sus dos primeros eventos persistidos y ColaboradorRegistrado lleva payload
                // rico -- Identificacion y NombreColaborador son sealed class con campos privados y
                // ConfigurarSerializacion (#348). El write-side instala ese resolver dentro de su
                // ConfigureMarten (ComposicionServicios.AgregarServiciosColaboradores); sin la MISMA
                // fuente aqui, el daemon leeria colaborador_registrado con STJ vanilla y reventaria
                // con NotSupportedException en runtime, no en el build.
                //
                // Serializador y resolver en una sola llamada, misma razon de forma que en
                // ControlHoras/Programacion: UseSystemTextJsonForSerialization construye un
                // serializador NUEVO y lo reemplaza, asi que invocarlo despues de un stj.Configure(...)
                // perderia el TypeInfoResolver en silencio ("la trampa del orden", issue #268).
                // EnumStorage.AsInteger/Casing.Default son los que fija el write-side via
                // AgregarConfiguracionMartenComandos (Cosmos.EventSourcing.CritterStack 2.3.1,
                // verificado por decompilacion) -- se declaran igual para no depender de un default
                // de Marten que un upgrade futuro podria mover.
                // Fuente unica con el write-side (MEF-ADR-0029): se invoca la misma clase, nunca
                // una copia.
                opts.UseSystemTextJsonForSerialization(EnumStorage.AsInteger, Casing.Default, jsonOptions =>
                {
                    var resolver = new DefaultJsonTypeInfoResolver();
                    ConfiguracionSerializacionColaboradores.ConfigurarResolver(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });

                // Issue #356: primera proyeccion concreta del dominio -- FichaColaborador, N1
                // (SingleStreamProjection<FichaColaborador, string>). Lifecycle Async es el canonico
                // del worker (MEF-ADR-0034 seccion 3); Inline exigiria justificacion explicita en el
                // issue, ausente aqui.
                opts.Projections.Add<FichaColaboradorProjection>(ProjectionLifecycle.Async);

                // Issue #357: segunda proyeccion concreta del dominio -- la PRIMERA receta N2
                // (MultiStreamProjection<CategoriaDeEtiquetas, string>) de este BC: eventos
                // EtiquetaAsignada de MUCHOS streams de ColaboradorAggregateRoot convergen en el
                // MISMO documento cuando comparten categoria normalizada
                // (skills/projections/modelos-marten.md). Mismo lifecycle canonico del worker
                // (MEF-ADR-0034 seccion 3), aditivo dentro del mismo AddMartenStore.
                opts.Projections.Add<CategoriaDeEtiquetasProjection>(ProjectionLifecycle.Async);

                // Issue #373 CA-5: indices para el listado QUERY ListarFichasColaborador
                // (segunda mitad del desglose de #356) -- ninguna proyeccion ni read model
                // nuevos, solo indices sobre la MISMA FichaColaborador ya registrada arriba.
                //
                // GIN sobre EtiquetasNormalizadas: el filtro AND por etiquetas del endpoint
                // resuelve con UNA operacion de containment JSONB (precedente #337 sobre
                // Bloques/SedeId). Verificado por spike propio (Marten 9.12.0 + Postgres 16,
                // EXPLAIN con enable_seqscan=off): Schema.For<FichaColaborador>()
                // .Index(x => x.EtiquetasNormalizadas, gin) crea el indice sobre la expresion
                // (data->>'EtiquetasNormalizadas')::jsonb -- el endpoint reproduce ese MISMO
                // shape de expresion via MatchesSql para que el planner efectivamente elija un
                // Bitmap Index Scan sobre este indice (confirmado con EXPLAIN), no un Seq Scan.
                //
                // Btree sobre VigenteHasta: acelera "VigenteHasta >= FechaReferencia" (CA-1),
                // el filtro de vigencia que aplica TODA consulta del listado.
                //
                // Btree sobre NombreCompleto: acelera el ORDER BY del keyset (CA-3,
                // OrderBy(NombreCompleto).ThenBy(Id) -- Id ya tiene su propio indice como
                // primary key del documento, sin necesidad de uno adicional).
                opts.Schema.For<FichaColaborador>()
                    .Index(x => x.EtiquetasNormalizadas, i => i.Method = IndexMethod.gin)
                    .Index(x => x.VigenteHasta)
                    .Index(x => x.NombreCompleto);

                // Issue #587: tercera proyeccion concreta del dominio -- DirectorioColaborador, N1
                // (SingleStreamProjection<DirectorioColaborador, string>), mismo stream que
                // FichaColaboradorProjection. Vista propia para ENCONTRAR a una persona por nombre o
                // identificacion (MEF-ADR-0041), no para verla en detalle (esa sigue siendo la
                // ficha). Mismo lifecycle canonico del worker (MEF-ADR-0034 seccion 3), aditivo
                // dentro del mismo AddMartenStore.
                opts.Projections.Add<DirectorioColaboradorProjection>(ProjectionLifecycle.Async);

                // Issue #587 CA-5: indices para el futuro QUERY colaboradores/directorio (#590) --
                // ningun EXPLAIN se corre en este issue (sin superficie HTTP que ejercer), quedan
                // declarados para que #590 los aproveche y los verifique.
                //
                // Btree sobre NumeroDocumento: acelera el filtro por numero suelto (el asistente
                // puede recibir solo "79879078", sin el prefijo de tipo de documento).
                //
                // GIN sobre TokensNombre (array de strings): acelera el filtro "contiene todos estos
                // tokens" del termino de busqueda por nombre, mismo criterio de containment que el
                // GIN de EtiquetasNormalizadas arriba (precedente #337/#373).
                //
                // Btree sobre NombreCompleto: acelera el ORDER BY de un eventual listado por nombre,
                // mismo criterio que el btree ya declarado sobre FichaColaborador.NombreCompleto.
                opts.Schema.For<DirectorioColaborador>()
                    .Index(x => x.NumeroDocumento)
                    .Index(x => x.TokensNombre, i => i.Method = IndexMethod.gin)
                    .Index(x => x.NombreCompleto);
            })
            // Registrar el store no basta: sin esta llamada el daemon queda apagado y ninguna
            // proyeccion se materializa. HotCold elige lider sobre advisory locks de PostgreSQL,
            // lo correcto para un Container App que Azure puede correr momentaneamente con mas de
            // una replica (MEF-ADR-0034 seccion 2).
            .AddAsyncDaemon(DaemonMode.HotCold);

        return services;
    }
}
