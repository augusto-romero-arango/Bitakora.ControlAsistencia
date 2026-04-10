Investiga un error o sintoma reportado en el entorno desplegado. Comunicate en **espanol**.

## Entrada

El sintoma esta en: $ARGUMENTS

Si `$ARGUMENTS` esta vacio, responde: `Uso: /bug [descripcion del sintoma]`

## Proceso

### 1. Validar prerequisitos

Verifica que el script de queries existe y es ejecutable:

```bash
test -x scripts/appinsights-query.sh && echo "OK" || echo "FAIL"
```

Si falla, responde:

```
El script scripts/appinsights-query.sh no existe o no es ejecutable.
Asegurate de que el issue #33 esta mergeado.
```

Verifica que hay sesion activa de Azure:

```bash
az account show --query name -o tsv 2>/dev/null
```

Si falla, responde:

```
No hay sesion activa de Azure. Ejecuta:
  az login
```

### 2. Lanzar el agente

Si ambas validaciones pasan, lanza el agente con el sintoma como contexto:

```bash
claude --agent bug-investigator "Sintoma reportado: $ARGUMENTS"
```

### 3. Instrucciones

Responde con:

```
Agente bug-investigator lanzado.
Sintoma: $ARGUMENTS

El agente investigara en App Insights, correlacionara con el codigo
y te presentara hipotesis antes de tomar accion.
```

## Reglas

- **No investigues nada tu mismo.** Solo valida y lanza el agente.
- **No modifiques codigo.** El agente tampoco puede hacerlo.
