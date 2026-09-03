#!/usr/bin/env bash
#
# Mide el tamaño del frontal construido contra DOS presupuestos, y falla si alguno se pasa.
#
# POR QUÉ DOS Y NO UNO
# --------------------
# Hasta el 0.16 esto era `du -sk --exclude='*.map' dist` contra un tope. El comentario de aquel
# paso decía —y decía bien— que se mide «lo que el navegador DESCARGA», y que por eso se excluyen
# los `.map`: «no se descarga al arrancar, así que contarlo medía otra cosa distinta de la que
# dice esta frase».
#
# Ese razonamiento dejó de aplicarse entero el día que las rutas pasaron a cargarse tarde (0.11).
# Vite emite un fragmento por ruta y el navegador se descarga TRES ficheros al arrancar; los otros
# siete llegan cuando alguien navega, o no llegan. Sumarlos todos vuelve a medir otra cosa distinta
# de la que dice la frase — y castiga justo lo que habría que premiar: partir una pantalla en su
# propio fragmento MEJORA el arranque y SUBE el número que la CI vigila.
#
#   arranque  = lo que `index.html` referencia (módulo de entrada, hoja de estilo, modulepreload)
#               más el propio `index.html`. Es lo que se paga antes de pintar nada.
#   total     = todo lo servido menos los `.map`. Vigila el crecimiento global sin castigar el
#               troceo: un paquete de 300 KiB que entre por descuido se nota aquí.
#
# Argumento entero, con las cifras del día que se decidió, en
# `docs/adr/adr-0028-el-presupuesto-mide-el-arranque-no-la-suma-de-los-fragmentos.md`.
#
# POR QUÉ BYTES Y NO `du`
# -----------------------
# `du -sk` redondea cada fichero al bloque del sistema de ficheros, así que da números distintos
# en la máquina de desarrollo y en el runner. Ya mordió en el 0.1 —1097 kB en local, 1104 en el
# runner— y aquella nota dejó escrito que «la cifra local no es la que decide». Sumando bytes, la
# cifra local SÍ es la que decide, y un ajuste de presupuesto se puede razonar sin gastar un run.
#
# POR QUÉ AFIRMA QUE HA MIRADO ALGO
# ---------------------------------
# Una medida que no encuentra ningún fichero da CERO, y cero pasa cualquier tope. Bastaría con que
# Vite cambiara la forma del `<script>`, o con que alguien moviera `index.html`, para que este paso
# midiera la nada y siguiera verde para siempre. Es la vacuidad del ADR-0020 con otra cara, así que
# el paso FALLA si el conjunto de arranque está vacío, si no lleva ningún `.js`, o si el arranque
# sale mayor que el total.
#
# Uso: presupuesto-del-frontal.sh <dist> [tope-arranque-KiB] [tope-total-KiB]

set -euo pipefail

DIST="${1:-frontend/dist}"
TOPE_ARRANQUE_KIB="${2:-450}"
TOPE_TOTAL_KIB="${3:-900}"

INDICE="$DIST/index.html"

fallar() {
  echo "::error::$1"
  echo "$1" >&2
  exit 1
}

anotar() {
  echo "$1"
  echo "::notice::$1"
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    echo "$1" >> "$GITHUB_STEP_SUMMARY"
  fi
}

# KiB redondeando HACIA ARRIBA: un byte de más ya es un KiB de más. Redondear hacia abajo regalaría
# hasta 1023 bytes por medida — poco, pero poco a favor de quien crece.
a_kib() {
  echo $((($1 + 1023) / 1024))
}

if [ ! -d "$DIST" ]; then
  fallar "no existe el directorio construido '$DIST': ¿se ha ejecutado el build?"
fi

if [ ! -f "$INDICE" ]; then
  fallar "no existe '$INDICE': sin él no se sabe qué descarga el navegador al arrancar."
fi

# --- Arranque: lo que `index.html` referencia, más él mismo -------------------------------------
#
# Se leen los `src=` y `href=` del documento y se resuelven contra `dist`. El patrón exige un
# ESPACIO delante del atributo, y no es un adorno: sin él, `data-src=` casa con `src=`. Lo destapó
# la primera mutación de este ítem, que pretendía romper el parseo y salió VERDE porque el parseo
# era demasiado ancho. Un atributo que solo TERMINA en `src` no es una descarga de arranque.
#
# Lo que no exista dentro de `dist` (una URL absoluta, un enlace externo) se descarta en silencio:
# no lo sirve esta imagen. Lo que sí exista se cuenta, sea `<script>`, `<link rel=stylesheet>` o
# `<link rel=modulepreload>` — los tres son descarga de arranque, y enumerar los `rel` que hoy usa
# Vite sería una lista que envejece sola.

bytes_arranque=$(wc -c < "$INDICE")
ficheros_arranque=("index.html")
tamanos_arranque=("$bytes_arranque")
javascript_en_arranque=0

while IFS= read -r referencia; do
  [ -n "$referencia" ] || continue

  relativa="${referencia#/}"
  camino="$DIST/$relativa"

  [ -f "$camino" ] || continue
  case "$relativa" in *.map) continue ;; esac

  bytes=$(wc -c < "$camino")
  bytes_arranque=$((bytes_arranque + bytes))
  ficheros_arranque+=("$relativa")
  tamanos_arranque+=("$bytes")

  case "$relativa" in *.js | *.mjs) javascript_en_arranque=1 ;; esac
done < <(grep -oE '[[:space:]](src|href)="[^"]*"' "$INDICE" |
  sed -E 's/^[[:space:]]*(src|href)="//; s/"$//')

# --- Total: todo lo servido menos los mapas -----------------------------------------------------

bytes_total=0
while IFS= read -r fichero; do
  bytes_total=$((bytes_total + $(wc -c < "$fichero")))
done < <(find "$DIST" -type f ! -name '*.map' | sort)

# --- Las tres afirmaciones de control, ANTES de comparar con los topes --------------------------

if [ "${#ficheros_arranque[@]}" -lt 2 ]; then
  fallar "el arranque solo tiene el propio index.html: ninguna de sus referencias se ha resuelto dentro de '$DIST'. La medida sería CERO y pasaría cualquier tope, así que se falla en vez de mentir."
fi

if [ "$javascript_en_arranque" -eq 0 ]; then
  fallar "el arranque no incluye ningún .js: el <script type=\"module\"> de index.html no se ha reconocido, y sin el módulo de entrada esta medida no es la del arranque."
fi

if [ "$bytes_arranque" -gt "$bytes_total" ]; then
  fallar "el arranque ($bytes_arranque B) sale mayor que el total ($bytes_total B): se está contando algo que no vive en '$DIST'."
fi

# --- El desglose, para que cada cifra diga de dónde sale ----------------------------------------

kib_arranque=$(a_kib "$bytes_arranque")
kib_total=$(a_kib "$bytes_total")

echo "Arranque — lo que el navegador pide antes de pintar nada:"
for i in "${!ficheros_arranque[@]}"; do
  printf '  %8s B  %s\n' "${tamanos_arranque[$i]}" "${ficheros_arranque[$i]}"
done

anotar "Frontal · arranque ${kib_arranque}/${TOPE_ARRANQUE_KIB} KiB en ${#ficheros_arranque[@]} ficheros · total servido ${kib_total}/${TOPE_TOTAL_KIB} KiB"

# --- Y solo ahora, los topes --------------------------------------------------------------------

excedido=0

if [ "$kib_arranque" -gt "$TOPE_ARRANQUE_KIB" ]; then
  echo "::error::El ARRANQUE del frontal supera su presupuesto (${kib_arranque} KiB > ${TOPE_ARRANQUE_KIB} KiB). Es lo que el usuario espera antes de ver nada: mirar si algo que debería cargarse tarde ha entrado como import estático."
  excedido=1
fi

if [ "$kib_total" -gt "$TOPE_TOTAL_KIB" ]; then
  echo "::error::El TOTAL servido del frontal supera su presupuesto (${kib_total} KiB > ${TOPE_TOTAL_KIB} KiB). Este tope no vigila el arranque sino el crecimiento global: mirar qué dependencia nueva ha entrado."
  excedido=1
fi

exit "$excedido"
