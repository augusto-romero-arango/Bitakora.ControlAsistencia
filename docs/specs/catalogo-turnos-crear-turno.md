# Especificacion: Crear Turno en el Catalogo de Turnos

**Dominio**: Programacion
**Aggregate**: CatalogoTurnos
**Modo sugerido para planner**: desglosar
**Origen**: sesion event-stormer 2026-04-05 (field notes: `docs/bitacora/field-notes/2026-04-05-0633-diseno-evento-turno-creado.md`)

---

## Problema

El dominio Programacion necesita su primer feature funcional: permitir al Programador de turnos crear turnos en el catalogo. Actualmente el dominio solo tiene scaffold (Program.cs, HealthCheck, assembly marker) sin logica de dominio.

Un turno es una plantilla reutilizable que define la estructura temporal del trabajo de un empleado en un dia. Se compone de franjas ordinarias de trabajo, cada una con posibles descansos y horas extras programadas contenidas dentro.

---

## Modelo de dominio descubierto

### Jerarquia de franjas (contencion)

```
Turno
├── FranjaOrdinaria (6:00 → 12:00)
│   ├── FranjaExtra (6:00 → 8:00)         ← extras programadas
│   └── FranjaDescanso (10:00 → 10:15)    ← pausa operativa
├── FranjaOrdinaria (14:00 → 16:00)
```

**Reglas de contencion:**
- FranjaOrdinaria puede contener FranjaDescanso y/o FranjaExtra
- FranjaOrdinaria NO puede contener otra FranjaOrdinaria
- FranjaDescanso y FranjaExtra son hojas — no contienen nada
- Descansos y extras deben estar dentro del rango temporal de su ordinaria padre
- Descansos y extras dentro de la misma ordinaria no se solapan entre si

### Clase base compartida

Todas las franjas comparten la estructura: `HoraInicio (TimeOnly)`, `HoraFin (TimeOnly)`, con calculo de duracion en minutos y conversores a horas, horas/minutos y horas decimales.

Pueden compartir una clase abstracta o interfaz. Cada tipo tiene su propia clase concreta.

### Semantica temporal

- **Inicio inclusivo, fin exclusivo**: franja 8:00-11:00 = exactamente 180 minutos
- **Offset de dia**: +0 (mismo dia, implicito por defecto) y +1 (dia siguiente, explicito)
- Ejemplo turno nocturno: FranjaOrdinaria (22:00 +0 → 06:00 +1)
- El factory infiere +1 cuando `fin < inicio` en la tupla del comando
- NO hay hora de corte — la particion legal por medianoche es responsabilidad de CalculoHoras

### Extras programadas vs no programadas

Las extras en el turno representan un compromiso del empleado (sabe que ese dia trabaja extras). Las extras no programadas (el jefe pidio quedarse) se resuelven en Depuracion/CalculoHoras.

---

## Flujo del feature

### 1. Endpoint HTTP recibe POST

El front envia un JSON con la estructura plana del turno.

### 2. Comando: CrearTurno

```
CrearTurno {
  TurnoId: Guid          // generado por el cliente (front)
  Nombre: string          // etiqueta de catalogo, no unico, no vacio
  Ordinarias: [(TimeOnly inicio, TimeOnly fin)]
  Descansos: [(TimeOnly inicio, TimeOnly fin)]
  Extras: [(TimeOnly inicio, TimeOnly fin)]
}
```

**Caracteristicas del comando:**
- Plano: tres arreglos separados de tuplas (inicio, fin)
- No hay campo "tipo" en las tuplas — el tipo es la propiedad del arreglo
- Las tuplas NO se reordenan — se respeta lo que envia el front
- El TurnoId lo genera el cliente para seguimiento asincrono (ej: SignalR)

### 3. Validaciones del endpoint HTTP

- JSON parseable
- Campos requeridos presentes (TurnoId, Nombre, Ordinarias)
- Tipos correctos (TurnoId es Guid valido, inicio/fin son horas validas)
- Nombre no vacio

### 4. Validaciones del command handler

- No existe un turno con ese TurnoId (idempotencia)
- Delega al factory del evento para construir el evento de dominio

### 5. Factory del evento TurnoCreado

El evento tiene un factory estatico que recibe el comando y encapsula toda la logica de construccion:

1. Detecta cuando `fin < inicio` → infiere offset +1 (cruce de medianoche)
2. Construye las FranjaOrdinaria como objetos de dominio
3. Para cada descanso y extra, busca la FranjaOrdinaria que lo contiene
4. Valida TODAS las invariantes (acumula errores, no falla en el primero)
5. Si hay errores → lanza una sola excepcion con todos los problemas
6. Si todo es valido → retorna el evento TurnoCreado construido

**Los mensajes de error deben ser descriptivos y guiar la correccion:**
- "El descanso (10:00-10:15) no esta contenido en ninguna franja ordinaria. Las franjas ordinarias son: (06:00-12:00), (14:00-16:00)"
- "La extra (09:00-11:00) se solapa con el descanso (10:00-10:15) dentro de la franja ordinaria (06:00-12:00)"

### 6. Evento: TurnoCreado

```
TurnoCreado {
  TurnoId: Guid
  Nombre: string
  FranjasOrdinarias: [
    {
      HoraInicio: TimeOnly
      HoraFin: TimeOnly
      DiaOffsetFin: int (0 o 1)
      Descansos: [{ HoraInicio, HoraFin, DiaOffsetInicio, DiaOffsetFin }]
      Extras: [{ HoraInicio, HoraFin, DiaOffsetInicio, DiaOffsetFin }]
    }
  ]
}
```

El evento es rico: tiene la estructura de contencion completa con offsets ya inferidos.

### 7. Aggregate: CatalogoTurnos (TurnoAggregateRoot)

Aplica el evento al estado. El aggregate guarda: TurnoId, Nombre, FranjasOrdinarias, EstaActivo.

### 8. Publicacion

El evento TurnoCreado se publica al topic `eventos-programacion` para que otros dominios puedan consumirlo si lo necesitan.

---

## Invariantes del aggregate

1. Un turno debe tener al menos una franja ordinaria
2. Las franjas ordinarias no pueden solaparse entre si
3. Los descansos deben estar contenidos dentro del rango temporal de su franja ordinaria padre
4. Las extras deben estar contenidas dentro del rango temporal de su franja ordinaria padre
5. Descansos y extras dentro de la misma ordinaria no pueden solaparse entre si
6. Una franja ordinaria solo puede contener descansos y extras (no otras ordinarias)
7. Descansos y extras son hojas — no contienen otras franjas
8. El nombre no puede estar vacio

---

## Representacion

`ToString()` genera: `"{nombre} (06:00-12:00)(14:00-16:00)"` — nombre seguido de las franjas ordinarias.

---

## Stack tecnico relevante

- **Event sourcing**: Marten + Wolverine (ADR-0006)
- **Patron de mensajes**: .resx per-aggregate con clase Mensajes anidada (ADR-0012)
- **Naming de eventos**: PascalCase participio pasado (ADR-0005)
- **Topic**: `eventos-programacion` (ADR-0005)
- **Contracts**: value objects compartidos van en `src/Bitakora.ControlAsistencia.Contracts/`
- **Dominio**: `src/Bitakora.ControlAsistencia.Programacion/`
- **Tests**: `tests/Bitakora.ControlAsistencia.Programacion.Tests/`

---

## Artefactos de dominio actualizados

- `docs/eda/aggregates/catalogo-turnos.yaml` — aggregate completo con value objects, invariantes, comando
- `docs/eda/ubiquitous-language.yaml` — terminos nuevos: Franja Ordinaria, Franja de Descanso, Franja de Extra, Offset de dia

---

## Preguntas abiertas (para el planner)

1. Como se implementa el patron de acumulacion de errores en el factory? (lista de ValidationError vs AggregateException vs resultado tipado?)
2. Necesitamos un limite maximo de franjas por turno?
3. El turno debe validar duracion maxima legal o eso es de otro dominio?
4. ToString() vive en el aggregate o en un value object Turno?
5. Los value objects de franja (FranjaOrdinaria, FranjaDescanso, FranjaExtra) van en Contracts o en el dominio Programacion?
