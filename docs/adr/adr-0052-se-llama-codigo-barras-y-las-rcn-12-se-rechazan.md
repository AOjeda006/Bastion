---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [ddd, catalogo, lenguaje-ubicuo, gs1]
tags: [adr, gtin, codigo-barras, nombres, plan-maestro, rcn, upc-e, prefijos, adr-0051]
revisado: 2026-10-02
---

# ADR-0052: Se llama `CodigoBarras`, como en el §7.3, y las RCN-12 también se rechazan

- **Estado:** aceptado
- **Fecha:** 2026-10-02
- **Sale del 2.10**, del primer paso de su dominio y de la revisión que se le hizo antes del commit.
- **Enmienda el ADR-0051** en dos cosas:
  - **los nombres**: el §2 (el agregado y su tabla), el §5 (el índice y su `type`), el §6 (los
    permisos) y el §11 (lo que decidió el agente);
  - **el §4**, la tabla de prefijos: entran las RCN-12 de la §2.1.11.3, el GTIN-8 se reconoce de
    otra forma, y tres motivos se cuentan mejor.

## Contexto

**El nombre.** El ADR-0051 llamó `ArticuloGtin` al agregado que guarda un GTIN de un artículo, y
`catalogo.articulos_gtin` a su tabla. Los dos nombres eran del agente, y el plan maestro ya tenía
uno. El §7.3 nombra los hijos del artículo así: «**CodigoBarras**, **ArticuloProveedor** (…),
**VarianteArticulo**». La decisión 13 de la puerta de la fase 2 lo repite: «`CodigoBarras` vive en
Catálogo», y esta fase trae «el GTIN de la ficha del artículo, varios por artículo (base, caja,
palé)». Es lo mismo que el 2.10 construye.

El modelo de dominio del §7 no se reabre (Anexo A.4). Un nombre distinto para el mismo concepto es
un sinónimo, y el glosario no los admite. El código sigue el §7 al pie de la letra: `Articulo`,
`ArticuloProveedor`, `LineaTarifa` y `MovimientoStock` se llaman como allí. El fallo se vio al
escribir la fila del glosario, que `ElGlosarioDelDominioTests` pidió en cuanto el agregado compiló.

**La tabla de prefijos.** Antes del primer commit, dos revisores contrastaron el *value object* con el
texto de las *GS1 General Specifications*, Release 26.0: uno con la especificación y otro con
mutaciones. El de la especificación encontró que el ADR-0051 §4 promete rechazar las RCN y no las
rechaza todas. La §2.1.11.3 reserva parte del U.P.C. 0 para la numeración interna de una empresa:
los códigos locales (LAC) y los de supresión de ceros de la tienda (RZSC), que viajan en UPC-E. «Como
cualquier empresa puede usarlos, no identifican un artículo de forma única si sale de sus
instalaciones». Las tablas 1-4 a 1-6 no lo enseñan, porque esa reserva cae dentro de prefijos que la
1-4 da para empresas. El `001000000052` es un LAC, y entraba.

## Decisión

### 1. Los nombres

- **El agregado es `CodigoBarras`**, en `Bastion.Catalogo.Domain.Catalogo`. Su propiedad `Gtin` es
  el *value object* del ADR-0051 §1, que conserva su nombre: un código de barras **lleva** un GTIN.
- **La tabla es `catalogo.codigos_barras`**. Se forma como `articulos_proveedor` y `lineas_tarifa`:
  se pone en plural la primera palabra.
- **El índice único es `ix_codigos_barras_gtin_uno_por_empresa`**, sobre `(empresa_id, gtin)`. Su
  `409` es `codigo-barras-duplicado`. Los `CHECK` empiezan por `ck_codigos_barras_`.
- **Los permisos son `catalogo.codigo-barras.agregar` y `catalogo.codigo-barras.quitar`**, como
  `catalogo.articulo-proveedor.agregar` sale de `ArticuloProveedor`.
- **Lo que no cambia:**
  - las rutas, que contestó el usuario en la puerta: `/api/v1/catalogo/articulos/{id}/gtins` y
    `/api/v1/catalogo/articulos/gtins/{id}`. El recurso es el GTIN que el código lleva, y así lo
    teclea quien lo busca;
  - la página, `/articulos/:id/gtin`;
  - los `type` de los motivos, que son del GTIN y empiezan por `gtin-`;
  - el enumerado `NivelDeGtin`: el nivel es la agrupación donde va impreso el GTIN.

### 2. La tabla de prefijos

- **Los LAC y los RZSC son de circulación restringida.** Se leen con la figura 2-1 de la §2.1.11.3,
  posición por posición, sobre el GTIN-12 que va en los catorce detrás de dos ceros:
  - un LAC es U.P.C. `001000` a `007999`, cuatro ceros en N7 a N10, y un 5 a 9 en N11;
  - un RZSC es U.P.C. `001000` a `005000`, cinco ceros en N4 a N8, y un 1 a 9 en N9.

  El control no se mira: la figura no lo limita. Un GTIN-14 con indicador construido sobre uno de
  ellos también se rechaza, porque la §2.1.7.2 prohíbe las RCN en un GTIN-14. Se rechazan los
  extremos de cada versión de la figura, y entra el vecino que se sale por una sola posición.
- **Un GTIN-8 se reconoce porque los trece empiezan por `00000`**, sin más condición. El ADR-0051 §4
  decía «sin ser `0000000`». Pero la fila `0000000` de la tabla 1-4 son los mismos números que el
  `000` a `099` de la 1-5, con el mismo motivo, así que la tabla 1-5 cubre a las dos. Se quita así
  una línea que ninguna mutación podía poner roja: quitarla daba el mismo motivo por el otro camino.
- **El `05` se sigue admitiendo, por otro motivo.** El ADR-0051 dijo que admitirlo era admitir un
  prefijo que nadie había recibido. No es así. El U.P.C. 5 fue el sistema de cupones de Norteamérica
  hasta 2011, cuando lo sustituyó el AI (8110) (§2.6.3.6). Hoy la tabla 1-4 lo da para prefijos de
  empresa, y un cupón de papel de hace quince años está caducado. Se admite por la tabla 1-4, que es
  la vigente.
- **El `951` no es «de nadie»: se dio para otra cosa.** De 2004 a 2012 sirvió para los números de
  gestor del identificador general del EPC, y GS1 dejó de darlos en junio de 2023 (tabla 1-4). Sigue
  rechazándose en trece como *sin asignar*: nunca fue un GTIN. **En un GTIN-8 se admite**, porque la
  tabla 1-5 lo incluye en el `300` a `951` de los GTIN-8.
- **Los cupones se citan donde están**: el `99` y del `981` al `983`, en la §2.6.3; el `980`, en la
  §2.6.4. La §2.6.2 es la del GCN, a donde GS1 los lleva.

## Consecuencias

- **El ADR-0051 lleva la nota en su *Estado* y al principio de los puntos que cambian**, y dice lo
  mismo que decía en todo lo demás.
- **Ningún nombre del ADR-0051 llegó a la base ni a `main`.** El agregado vivió en la rama, sin
  migración y sin commit propio, así que no hay nada que renombrar fuera del código.
- **La tanda de mutaciones del 2.10 sabe de antemano cuál es equivalente.** Al pasar de una línea
  `984`–`989` a `983`–`989`, la fila de los cupones del `980` al `983` la tapa, así que no se puede
  poner roja. Se anota como equivalente en la tabla, con su motivo, en vez de contarla como hallazgo.
- **Lo que se aprende:**
  - un nombre de clase de dominio se busca en el §7 antes de escribirlo, aunque el encargo hable
    del concepto con otra palabra;
  - la tabla de un estándar no es el estándar entero. Una reserva puede vivir en el texto de una
    sección y no en la tabla que resume los prefijos. Por eso la tabla se contrasta con el texto, y
    lo hace alguien que no la escribió.

## Procedencia

El §7.3 del plan maestro y la decisión 13 de la puerta de la fase 2 (PLAN, *Decisiones tomadas*).
Las *GS1 General Specifications*, Release 26.0, Ratified, Jan 26: §1.2.3.1 (tabla 1-4), §1.2.3.2
(tabla 1-5), §2.1.7.2, §2.1.11.3 y su figura 2-1, §2.6.3.6 y §2.6.4, leídas de
<https://ref.gs1.org/standards/genspecs/> el 2026-10-02. El agente corrige el nombre sin preguntar
porque no hay nada que elegir: el modelo de dominio del §7 no se reabre. Rechazar las RCN-12 es
cumplir lo que el ADR-0051 §4 ya decía, y es lo estricto, que es lo reversible.
