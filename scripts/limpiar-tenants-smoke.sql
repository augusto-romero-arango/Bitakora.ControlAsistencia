-- Limpieza semanal de los tenants de smoke en dev (issue #994, CA-ADR-0040).
-- Uso: psql -v ON_ERROR_STOP=1 -v schema=<schema> -f scripts/limpiar-tenants-smoke.sql
-- Una transaccion por schema. Reglas:
--  * Solo tenant_id = 'tenant-smoke' o LIKE 'tenant-smoke-%'. Toda sentencia DELETE lleva ese filtro.
--  * Limite del daemon: solo streams cuyo ultimo seq_id es estrictamente menor que
--    min(last_seq_id) de {schema}.mt_event_progression. Sin progresion, no se borra nada.
--  * Antiguedad: streams con mt_streams.timestamp > 24 h; documentos mt_doc_* con mt_last_modified > 24 h.
--  * Orden: mt_events y luego mt_streams (FK). Nunca TRUNCATE.
-- Salida: lineas "RESULT|schema|tabla|filas" y "LIMITE|schema|seq_id" via RAISE NOTICE.
BEGIN;
SELECT set_config('smoke.schema', :'schema', true) AS smoke_schema \gset

DO $limpieza$
DECLARE
    v_schema  text := current_setting('smoke.schema');
    v_limite  bigint;
    v_n       bigint;
    v_tabla   text;
BEGIN
    IF v_schema NOT IN ('programacion', 'control_horas', 'colaboradores', 'sedes') THEN
        RAISE EXCEPTION 'Schema no permitido: %', v_schema;
    END IF;

    IF to_regclass(format('%I.mt_event_progression', v_schema)) IS NULL
       OR to_regclass(format('%I.mt_events', v_schema)) IS NULL
       OR to_regclass(format('%I.mt_streams', v_schema)) IS NULL THEN
        RAISE NOTICE 'OMITIDO|%|sin tablas de eventos o de progresion', v_schema;
        RETURN;
    END IF;

    EXECUTE format('SELECT min(last_seq_id) FROM %I.mt_event_progression', v_schema) INTO v_limite;
    IF v_limite IS NULL THEN
        RAISE NOTICE 'OMITIDO|%|mt_event_progression vacia', v_schema;
        RETURN;
    END IF;
    RAISE NOTICE 'LIMITE|%|%', v_schema, v_limite;

    EXECUTE format($q$
        CREATE TEMP TABLE elegibles ON COMMIT DROP AS
        SELECT s.tenant_id, s.id AS stream_id
        FROM %1$I.mt_streams s
        WHERE (s.tenant_id = 'tenant-smoke' OR s.tenant_id LIKE 'tenant-smoke-%%')
          AND s.timestamp < now() - interval '24 hours'
          AND (SELECT max(e.seq_id) FROM %1$I.mt_events e
               WHERE e.tenant_id = s.tenant_id AND e.stream_id = s.id) < %2$L
    $q$, v_schema, v_limite);

    -- Guarda en tiempo de ejecucion (CA-2): ningun stream elegible fuera de tenant-smoke*.
    IF EXISTS (SELECT 1 FROM elegibles
               WHERE NOT (tenant_id = 'tenant-smoke' OR tenant_id LIKE 'tenant-smoke-%')) THEN
        RAISE EXCEPTION 'Guarda: stream elegible fuera de tenant-smoke en %', v_schema;
    END IF;

    EXECUTE format($q$
        DELETE FROM %1$I.mt_events e USING elegibles x
        WHERE (e.tenant_id = 'tenant-smoke' OR e.tenant_id LIKE 'tenant-smoke-%%')
          AND e.tenant_id = x.tenant_id AND e.stream_id = x.stream_id
    $q$, v_schema);
    GET DIAGNOSTICS v_n = ROW_COUNT;
    RAISE NOTICE 'RESULT|%|mt_events|%', v_schema, v_n;

    EXECUTE format($q$
        DELETE FROM %1$I.mt_streams s USING elegibles x
        WHERE (s.tenant_id = 'tenant-smoke' OR s.tenant_id LIKE 'tenant-smoke-%%')
          AND s.tenant_id = x.tenant_id AND s.id = x.stream_id
    $q$, v_schema);
    GET DIAGNOSTICS v_n = ROW_COUNT;
    RAISE NOTICE 'RESULT|%|mt_streams|%', v_schema, v_n;

    FOR v_tabla IN
        SELECT t.table_name
        FROM information_schema.tables t
        WHERE t.table_schema = v_schema
          AND t.table_type = 'BASE TABLE'
          AND t.table_name LIKE 'mt\_doc\_%'
          AND EXISTS (SELECT 1 FROM information_schema.columns c
                      WHERE c.table_schema = t.table_schema AND c.table_name = t.table_name
                        AND c.column_name = 'tenant_id')
          AND EXISTS (SELECT 1 FROM information_schema.columns c
                      WHERE c.table_schema = t.table_schema AND c.table_name = t.table_name
                        AND c.column_name = 'mt_last_modified')
        ORDER BY t.table_name
    LOOP
        EXECUTE format($q$
            DELETE FROM %1$I.%2$I
            WHERE (tenant_id = 'tenant-smoke' OR tenant_id LIKE 'tenant-smoke-%%')
              AND mt_last_modified < now() - interval '24 hours'
        $q$, v_schema, v_tabla);
        GET DIAGNOSTICS v_n = ROW_COUNT;
        RAISE NOTICE 'RESULT|%|%|%', v_schema, v_tabla, v_n;
    END LOOP;
END
$limpieza$;
COMMIT;
