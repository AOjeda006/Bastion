#!/usr/bin/env bash
# El canal de la consola del frontal, con su centinela (ADR-0060).
#
# Lee el registro de `npm run test` y decide dos cosas:
#
#   1. Que la marca del caso centinela (`frontend/src/app/ElCanalDeLaConsola.test.ts`) está. El
#      caso la escribe con `console.error` desde un caso en verde, que es el camino de un aviso de
#      `act()`. Si no sale, el informe se está callando la consola de los casos en verde, y el cero
#      de abajo no mide nada (ADR-0058).
#   2. Que no hay ni un solo aviso de `act()`. El fondo está a cero desde el 1.8 (`AGENTS.md`), así
#      que uno es un hallazgo, y no se cuenta contra ningún umbral.
#
# El registro va a un fichero y `grep` lee el fichero: un `grep -q` al final de una tubería, bajo
# `pipefail`, corta la tubería y da rojos por azar en Linux (ítem 1.12).
#
# Uso: bash scripts/ci/canal-de-la-consola.sh <registro de npm run test>
set -euo pipefail

REGISTRO="${1:?uso: canal-de-la-consola.sh <registro de npm run test>}"

# La misma cadena que escribe el caso centinela. Si dejan de coincidir, la marca no se encuentra y
# el paso falla.
MARCA='CENTINELA DEL CANAL DE LA CONSOLA'
AVISO='not wrapped in act'

if [ ! -s "$REGISTRO" ]; then
  echo "::error title=Canal de la consola::No hay registro de los tests en $REGISTRO, o está vacío."
  exit 1
fi

# `grep -c` sale con 1 cuando cuenta cero, y aquí cero es una respuesta, no un fallo.
MARCAS="$(grep -c -F -- "$MARCA" "$REGISTRO" || true)"
AVISOS="$(grep -c -F -- "$AVISO" "$REGISTRO" || true)"

if [ "$MARCAS" -eq 0 ]; then
  echo "::error title=Canal de la consola::El canal está mudo: la marca del caso centinela no" \
    "sale en el registro, así que tampoco saldría un aviso de act(). ¿Se ha quitado el informe" \
    "fijado de frontend/vite.config.ts, o se ha pasado un --reporter? (ADR-0058, ADR-0060)"
  exit 1
fi

if [ "$AVISOS" -ne 0 ]; then
  echo "::error title=Canal de la consola::Avisos de act() en el registro: $AVISOS. El fondo está" \
    "a cero desde el 1.8, así que cada uno es un hallazgo: qué efecto se quedó fuera de un act()," \
    "o qué aserción se hizo sobre algo que todavía no había ocurrido."
  grep -n -F -m 5 -- "$AVISO" "$REGISTRO"
  exit 1
fi

echo "::notice title=Canal de la consola::El canal está abierto, con la marca en el registro" \
  "($MARCAS), y no hay ningún aviso de act()."
