---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, sql, inventario, concurrencia, dinero]
tags: [adr, r3, pmp, valoracion, fecha-de-operacion, cerrojo, adr-0046]
revisado: 2026-09-29
---

# ADR-0047: Ninguna fecha anterior al último movimiento de su clave

- **Estado:** aceptado
- **Fecha:** 2026-09-29
- **Sale del addendum del 2.8**, que el usuario encargó el 2026-09-29 tras verificar el ítem. El
  criterio lo fijó el usuario; los medios, el agente.
- **Enmienda el ADR-0046 §5, su último punto** («El orden de valoración es el de confirmación, no el
  de la fecha de operación»). El orden sigue siendo el de confirmación. Lo que cambia es que, dentro
  de cada clave, la fecha ya no puede ir hacia atrás, así que los dos órdenes coinciden.

## Contexto

El ADR-0046 valora en orden de confirmación, y deja que el valor de una fecha pasada sea la suma
del `valor` de las filas del libro hasta esa fecha. Cada cosa por separado es razonable. Juntas dan
cifras imposibles, y el usuario lo enseñó con un contraejemplo:

- el día 10 entran 10 unidades a 10 €;
- el día 20 entran 10 a 100 €, y el precio medio queda en 55;
- después se confirma una salida de 10, fechada el día 15.

La salida se valora a 55, el precio de cuando se confirma, y resta 550 €. Sumando hasta el día 15,
la clave queda con 0 unidades y −450 €. Si la salida se fecha antes de la primera entrada, salen −10
unidades y −550 €. La tabla prohíbe los dos estados, pero solo mira el de ahora, no el de cada fecha.

**El usuario decide cerrarlo en origen**: un documento no puede llevar una fecha anterior al último
movimiento de alguna de sus claves.

## Decisión

### 1. La valoración guarda la fecha de su último movimiento

`inventario.valoraciones` gana `ultima_fecha`, de tipo `date`: la fecha de operación más alta de las
filas del libro de su clave. **Admite nulo**, porque la fila que nace en el cerrojo todavía no tiene
ningún movimiento (ADR-0046 §2, «bloquear es crear»). La sentencia que suma la pone, y ninguna otra
la escribe.

**La migración la rellena con el máximo del libro por clave**, con un `UPDATE` sobre las
valoraciones y una lectura agrupada del libro, que no se toca. Añadir una columna que admite nulo y
no tiene valor por omisión no reescribe la tabla. El libro de antes puede tener fechas atrasadas, y
la migración no las arregla: es de solo añadido. La regla vale desde la primera confirmación que
pase por ella.

### 2. El dominio lo decide contra la fila ya bloqueada

`SaldoValorado` gana `UltimaFecha`, y `LoQueImpide` y `Valorar` reciben la fecha de operación del
documento. `MotivoDelImpedimento` gana `FechaAnteriorAlUltimoMovimiento`, y el borde lo traduce a
`ajuste-fecha-anterior-al-ultimo-movimiento`, un `422` de regla de negocio. El impedimento lleva la
última fecha, para que el mensaje la diga.

**Se mira contra la fila bloqueada, no contra el libro.** Consultar el máximo del libro antes del
cerrojo deja una ventana: dos documentos de la misma clave, uno del día 20 y otro del día 15, se
leerían sin ver al otro, y los dos pasarían. Con la fila bloqueada, el del día 15 espera al del día
20, lee la fecha que este dejó y recibe el `422`. La lectura es la que ya hacía el ADR-0046 §2, con
una columna más, así que la regla no añade cerrojos ni cambia su orden.

**Se mira la primera, antes de la divisa**, clave por clave. Un documento con fecha atrasada no se
puede confirmar en esa fecha pase lo que pase con la divisa, así que es lo primero que se le dice.

**La misma fecha vale.** La regla es «anterior», no «anterior o igual»: dos documentos del mismo día
sobre la misma clave son lo normal.

### 3. La sentencia que suma se niega a mover la fila hacia atrás

La guarda va en el `WHERE` de la sentencia que suma, como la de la divisa (ADR-0046 §7): una clave
cuya `ultima_fecha` sea posterior a la fecha más temprana de sus filas nuevas no se toca. El
recuento lo denuncia, y el caso de uso se estrella con una `InvalidOperationException`, que es un
defecto y no un desenlace. Llegar ahí es saltarse el dominio, y eso no se traduce a un `422`.

**No es un `CHECK`, porque un `CHECK` no puede verlo.** PostgreSQL no admite restricciones `CHECK`
que miren algo fuera de la fila nueva o actualizada (documentación de PostgreSQL, *Constraints →
Check Constraints*, `ddl-constraints.html`). El valor de antes de la fila es justo lo que no ve, y
la regla es sobre él. Un disparador sí podría, y el proyecto los usa para que el libro sea de solo
añadido (ADR-0044). Aquí no hace falta: la tabla solo la escribe esta clase, en tres sentencias, y
la guarda está en la única que mueve la fecha.

Las fechas viajan como `date[]`, un `DateOnly` por fila. Npgsql escribe `DateOnly` como `date`
desde su versión 6 (documentación de Npgsql, *Date and Time Handling*, `types/datetime.html`), y el
proyecto usa la 10.

### 4. Lo que sigue permitido

- **La misma fecha**, como dice el punto 2.
- **Otra clave, u otro almacén del mismo artículo.** La fecha es de cada valoración, y la clave es
  la empresa, el artículo y el almacén (ADR-0046 §3). Atrasar un documento es posible mientras
  ninguna de sus claves se haya movido después.
- **Anular hoy.** El inverso lleva su propia fecha, la de hoy en UTC, y ninguna fecha del libro pasa
  de hoy: la fecha futura se rechaza antes de llegar aquí. Así que anular nunca choca con esta
  regla.

### 5. El último punto del ADR-0046 §5, enmendado

El orden de valoración sigue siendo el de confirmación. Pero ahora, dentro de cada clave, cada
confirmación lleva una fecha igual o posterior a la anterior. **Las filas de una clave con fecha
hasta un día cualquiera son siempre un principio de su secuencia de confirmaciones**, así que su
suma es el estado en que la dejó la última confirmación fechada ese día o antes. Ese estado existió,
y las tres restricciones de la tabla lo miraron.

De ahí sale el invariante que la propiedad comprueba: para cada clave y cada fecha del libro, la
suma hasta esa fecha no deja cantidad negativa, ni valor negativo, ni valor sin cantidad.

Y de ahí sale también que **el 2.14 ya no tiene que avisar en su pantalla** de que el valor de una
fecha pasada usa precios de otro momento. Es el que la clave tuvo de verdad, para todo lo que se
confirme desde este ADR. El libro de antes, si trae atrasos, sí puede sumar un estado que no
existió. El 2.14 decide qué hace con eso.

### 6. Lo que no cambia

- **La maquinaria de las instantáneas del 2.7** (ADR-0044) se queda entera. El corte es de la
  empresa, y la regla es de la clave: un movimiento posterior al último de su clave puede ser
  anterior al corte, y tiene que seguir invalidando las instantáneas que toca.
- **El ejercicio cerrado y la fecha futura** se comprueban antes, como siempre.
- **El stock negativo** lo sigue rechazando el motor, con el `CHECK` de la existencia.

### 7. Los casos y las mutaciones

- **El contraejemplo**, ahora un `422`, sin nada escrito: ni libro, ni existencia, ni valoración.
- **La misma fecha** se confirma.
- **Otra clave**, y **otro almacén del mismo artículo**, con fecha atrasada, se confirman.
- **Dos transacciones de verdad con las fechas cruzadas.** La del día 20 tiene el cerrojo, y la del
  día 15 espera y recibe el `422`.
- **La guarda de la sentencia**, llamada saltándose el dominio, se estrella como un defecto.
- **La migración** rellena la fecha con el máximo del libro por clave, y deja nula la de una clave
  sin movimientos.
- **La propiedad** gana el invariante del punto 5, y su generador modela el rechazo, como ya hacía
  con el stock.
- **La mutación que el encargo pide** quita a la vez la comprobación del dominio y la guarda de la
  sentencia, y la propiedad tiene que ponerse roja. Cada una por separado, y los bordes de la
  comparación, tienen también su mutación.

## Consecuencias

- **Un ajuste atrasado se rechaza si su clave se movió después.** Es una restricción nueva para el
  usuario, y el mensaje dice cuál fue el último movimiento para que sepa qué fecha puede usar.
- **La R3 lo dice en su fila**: el valor de una fecha pasada es la suma hasta esa fecha, y esa suma
  cumple las guardas de la tabla.
- **Los tests que atrasaban fechas sobre una clave movida después** se arreglan con otra clave u
  otra fecha, nunca aflojando la regla, y el PLAN los nombra.
- **Los tests que comparan un saldo entero** comparan también su fecha, porque ahora es parte de él.
- **Lo que se deja abierto**: el cuadre no compara `ultima_fecha` con el máximo del libro. La
  propiedad sí lo hace. Llevarlo al cuadre es una línea más en su lectura, y se propone al usuario.

## Procedencia

El criterio, el contraejemplo, la columna, el rechazo, la guarda de la sentencia, lo que sigue
permitido y los casos los fijó el usuario en el encargo del 2026-09-29. El agente decidió que la
columna admite nulo, el orden frente a la divisa, que el impedimento lleve la fecha y la mutación
de cada guarda por separado.
