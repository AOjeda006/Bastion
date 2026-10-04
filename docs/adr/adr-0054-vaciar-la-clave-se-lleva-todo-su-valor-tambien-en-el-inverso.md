---
tipo: referencia
stack: [dotnet, postgresql]
aplica_a: [ddd, inventario, dinero]
tags: [adr, transferencia, anulacion, inverso, valoracion, pmp, tope, adr-0046, adr-0053]
revisado: 2026-10-04
---

# ADR-0054: Vaciar la clave se lleva todo su valor, también en el inverso, aunque sea más del que entró

- **Estado:** aceptado
- **Fecha:** 2026-10-04
- **Sale del 2.11**, de la revisión que se hizo a la persistencia de la transferencia antes de su
  commit. El código no cambia: lo que se corrige son tres frases que la regla de la tabla ya
  contradecía.
- **Enmienda el ADR-0053** en el §5 (lo que se lleva la salida del destino al anular una recibida)
  y en el §6 (lo que recibe el origen), y **el ADR-0046 §6** en su primer punto (cuándo suma cero el
  par).

## Contexto

El ADR-0046 §5 fija cómo se valora el inverso de una entrada: `−min(v, V)`, con `v` el valor de la
línea original y `V` el que queda en la clave. **Si deja la clave a cero, se lleva `−V` entero.** La
última regla existe porque el motor no admite valor sin cantidad
(`ck_valoraciones_sin_cantidad_no_hay_valor`), y `ElPrecioMedioPonderado` la aplica igual a toda
salida que vacía su clave.

Tres frases la resumían mal, porque solo tenían en cuenta el tope:

- el ADR-0046 §6: «El par suma cero en valor siempre que el tope no toque»;
- el ADR-0053 §5: «La salida del destino lleva el valor que entró, y como mucho el que queda»;
- el ADR-0053 §6: «Sin tope, es exactamente lo que salió del origen. Con tope, es menos».

Las tres fallan en el mismo caso: el inverso vacía la clave, y lo que queda en ella vale **más** de
lo que entró. Pasa cuando, entre el original y la anulación, han entrado unidades más caras y han
salido otras.

**El caso**, con una transferencia:

1. El origen A tiene 5 unidades que valen 50, y el destino B tiene 10 que valen 200.
2. Se envían las 5 de A a B. Salen por 50, y A queda a cero.
3. Al recibir, B tiene 15 unidades por 250.
4. Un ajuste saca 10 de B a su precio medio, 16,666667. Son 166,6667, y B queda con 5 unidades por
   83,3333.
5. Al anular la transferencia, la salida del destino vacía B. Se lleva 83,3333, no 50, y eso es lo
   que recibe el origen.

## Decisión

### 1. Manda la regla de la tabla, y las tres frases se leen con ella

La salida del inverso se lleva el valor que compensa, como mucho el que queda, y **todo el que
queda si vacía la clave**. Así que puede quedar por debajo del valor que compensa, por el tope, o
por encima, porque vacía la clave. En los dos casos la línea del inverso guarda las dos cifras,
`ValorQueCompensa` y `Valor`, y la diferencia queda escrita.

### 2. El origen recibe lo que salió del destino, sea más o sea menos

Es la tercera respuesta de la puerta del 2.11, «Lo que salió del destino», y no cambia. Cambia la
frase que la resumía: si el inverso vacía el destino, el origen puede recibir **más** de lo que
salió de él, y su precio medio sube.

### 3. El invariante no cambia

El valor de la empresa es la suma de `valor` y `valor_en_transito` de todas sus valoraciones, y
solo lo mueven los ajustes (ADR-0053 §6). Lo que el inverso se lleva de más del destino entra entero
en el origen. La propiedad del 2.11 comprueba ese invariante, y no una cota por línea.

En el ajuste es distinto, porque sí mueve el valor de la empresa. Si un inverso que vacía la clave
se lleva más de lo que entró, el par no suma cero, y la diferencia queda en la línea como cuando
toca el tope.

## Consecuencias

- **El código no cambia.** `ElPrecioMedioPonderado` ya aplicaba la regla, y
  `Transferencia.ConfirmarComoInverso` ya exigía que al origen volviera lo que salió del destino, sin
  cota. Cambian dos comentarios del dominio que repetían la frase del tope, los de
  `LineaDeTransferencia.Valor` y `Transferencia.LineasAValorarEnElDestino`.
- **Un caso del dominio lo fija**:
  `ElInversoDeLaTransferenciaTests.Si_el_inverso_vacia_el_destino_el_origen_recibe_mas_de_lo_que_salio`,
  con los números de arriba.

## Procedencia

La revisión adversarial del paso 4 del 2.11, el 2026-10-04, encontró el caso con números. Las tres
frases eran del agente, y la regla de la tabla, del ADR-0046.
