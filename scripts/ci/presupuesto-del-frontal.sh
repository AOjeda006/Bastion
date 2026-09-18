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
#               más el propio `index.html`, MÁS los fragmentos declarados que se pagan siempre
#               antes del primer pintado sin estar en el `index.html`. Es lo que se paga antes de
#               pintar nada.
#   total     = todo lo servido menos los `.map`. Vigila el crecimiento global sin castigar el
#               troceo: un paquete de 300 KiB que entre por descuido se nota aquí.
#
# LA PARTE DECLARADA, Y POR QUÉ NO SE ADIVINA
# -------------------------------------------
# Hasta el ítem 2.1 «lo que `index.html` referencia» y «lo que se paga antes de pintar» eran la
# misma cosa, y esta cabecera lo decía así. Dejaron de serlo el día que el diccionario del idioma
# pasó a importación dinámica: el navegador lo pide ANTES de pintar —el arranque lo espera— y Vite
# no lo anuncia en el `index.html`, porque un `import()` que se resuelve en tiempo de ejecución no
# tiene sitio ahí. La medida se quedó 18 kB por debajo de lo que el usuario paga, y un tope que mide
# de menos es un tope que no avisa.
#
# Qué fragmentos son esos NO se puede deducir del artefacto: en `dist` un fragmento diferido de una
# ruta y uno que el arranque espera se parecen exactamente. Así que se DECLARAN aquí, en una lista
# cerrada, y la lista se compara en los dos sentidos contra la fuente que manda: lo declarado que no
# esté en `dist` es rojo, y lo que esté en `dist` cumpliendo la convención sin que nadie lo haya
# declarado, también. Sin las dos direcciones, el guardián envejece apuntando a un fichero que se
# renombró y sigue verde.
#
# Argumento entero, con las cifras del día que se decidió, en
# `docs/adr/adr-0036-el-presupuesto-cuenta-lo-que-se-paga-aunque-no-este-en-el-indice.md`, que
# enmienda la definición de arranque del **ADR-0028** —lo demás de aquel ADR sigue en pie—.
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
# La parte declarada hereda esa disciplina entera, y hace falta: una enmienda que se midiera a sí
# misma midiendo la nada sería peor que no haberla escrito, porque el número volvería a ser el de
# antes con una explicación nueva encima. Así que el paso FALLA también si la lista declarada está
# vacía, si alguno de sus miembros no resuelve en EXACTAMENTE un fichero de `dist`, o si al terminar
# no se ha resuelto ni uno.
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

# --- Y lo que se paga igual sin estar en el índice: la lista declarada ---------------------------
#
# UN GRUPO POR CONJUNTO DE ALTERNATIVAS MUTUAMENTE EXCLUYENTES, y se cuenta LA MAYOR. Un usuario
# paga UN diccionario y no dos: el tope es una promesa sobre el PEOR CASO que alguien puede llegar a
# pagar, no sobre el caso medio ni sobre la suma. Se dice aquí y no se deja implícito porque el día
# que entre un tercer idioma más gordo que los dos de hoy, lo que significa el número cambiaría en
# silencio; así, sube solo.
#
# Hoy hay un grupo y son los diccionarios de idioma (ítem 2.1). La convención del nombre la pone
# Vite: el fragmento se llama como el módulo que lo origina, o sea `assets/<idioma>-<hash>.js`.
#
# Y ESTE GUARDIÁN ES, ADEMÁS, LO QUE DEFIENDE LA PARTICIÓN DEL 2.1, que hasta hoy solo defendía un
# comentario. Si el diccionario vuelve al fragmento de entrada no habrá fragmento que encontrar y el
# paso se pone ROJO, en vez de tragarse 18 kB por debajo del tope y salir verde con 409 sobre 450.
# No es una hipótesis: el linter empuja hacia ahí. `consistent-type-imports` está como `error` con
# `fixStyle: inline-type-imports`, así que el día que alguien necesite importar un VALOR de `es.ts`
# en un módulo donde también se usa el tipo, la regla exige la forma mezclada, el arreglo automático
# la escribe y los 18 kB vuelven sin que nadie lo haya decidido.
IDIOMAS_DECLARADOS=("es" "en")

# La otra fuente. La lista declarada de arriba no se compara contra sí misma: se compara contra
# quien manda de verdad sobre cuántos idiomas hay, que es el código del frontal.
FUENTE_DE_IDIOMAS="$(dirname "$DIST")/src/app/i18n/idioma.ts"

if [ "${#IDIOMAS_DECLARADOS[@]}" -eq 0 ]; then
  fallar "la lista declarada de fragmentos de arranque está vacía: esta comprobación se estaría midiendo a sí misma midiendo la nada, y el número volvería a ser el de antes con una explicación nueva encima."
fi

if [ ! -f "$FUENTE_DE_IDIOMAS" ]; then
  fallar "no existe '$FUENTE_DE_IDIOMAS', que es la fuente contra la que se compara la lista declarada. Sin ella la comparación tendría un solo lado, que es no comparar."
fi

# El `|| true` no es descuido, y lo destapó una mutación de este mismo ítem: con `set -e`, una
# asignación cuyo comando devuelve 1 —y `grep` sin coincidencias devuelve 1— MATA el guion ahí
# mismo, antes de llegar al `if` de abajo. El resultado era un paso rojo sin una sola línea de
# diagnóstico, que es peor que el fallo: el rojo hay que leerlo.
linea_de_idiomas=$(grep -E 'export const IDIOMAS[[:space:]]*=' "$FUENTE_DE_IDIOMAS" | head -1 || true)

if [ -z "$linea_de_idiomas" ]; then
  fallar "no se ha encontrado 'export const IDIOMAS' en '$FUENTE_DE_IDIOMAS': el patrón ha dejado de casar y la comparación saldría verde contra una lista vacía."
fi

idiomas_del_codigo=()
while IFS= read -r idioma; do
  [ -n "$idioma" ] || continue
  idiomas_del_codigo+=("$idioma")
done < <(printf '%s' "$linea_de_idiomas" | grep -oE "'[a-zA-Z-]+'" | tr -d "'")

if [ "${#idiomas_del_codigo[@]}" -eq 0 ]; then
  fallar "'export const IDIOMAS' está en '$FUENTE_DE_IDIOMAS' pero no se le ha sacado ni un idioma: el patrón casa con la línea y no con su contenido."
fi

declarados_en_orden=$(printf '%s
' "${IDIOMAS_DECLARADOS[@]}" | sort)
del_codigo_en_orden=$(printf '%s
' "${idiomas_del_codigo[@]}" | sort)

if [ "$declarados_en_orden" != "$del_codigo_en_orden" ]; then
  fallar "la lista declarada de diccionarios de arranque y la de IDIOMAS en '$FUENTE_DE_IDIOMAS' no coinciden. Declarados: $(echo "$declarados_en_orden" | tr '
' ' '). En el código: $(echo "$del_codigo_en_orden" | tr '
' ' '). Un idioma nuevo cuyo diccionario nadie declara se descarga antes de pintar y no lo cuenta nadie."
fi

# Resueltos contra `dist`, uno a uno, y se cuenta el mayor.
mayor_del_grupo=0
fichero_del_grupo=""
detalle_del_grupo=""
resueltos=0

for idioma in "${IDIOMAS_DECLARADOS[@]}"; do
  coincidencias=()
  while IFS= read -r encontrado; do
    [ -n "$encontrado" ] || continue
    coincidencias+=("$encontrado")
  done < <(find "$DIST" -type f -name "${idioma}-*.js" ! -name '*.map' | sort)

  if [ "${#coincidencias[@]}" -ne 1 ]; then
    fallar "el diccionario declarado '$idioma' resuelve en ${#coincidencias[@]} ficheros dentro de '$DIST' y tiene que resolver en UNO. Si son cero, o el idioma ya no existe o su diccionario ha vuelto al fragmento de entrada —que es lo que el ítem 2.1 sacó de ahí, y son unos 18 kB que el navegador se sigue descargando antes de pintar—. Si son varios, el nombre ha dejado de identificar al fragmento."
  fi

  resueltos=$((resueltos + 1))
  camino="${coincidencias[0]}"
  relativa="${camino#"$DIST"/}"
  bytes=$(wc -c < "$camino")

  # Si el índice YA lo referencia —un `modulepreload` que Vite emitiera algún día—, ya está contado
  # arriba, y contarlo otra vez inflaría el arranque con un fichero que se descarga una sola vez.
  ya_contado=0
  for contado in "${ficheros_arranque[@]}"; do
    if [ "$contado" = "$relativa" ]; then
      ya_contado=1
    fi
  done

  if [ -n "$detalle_del_grupo" ]; then
    detalle_del_grupo="$detalle_del_grupo, "
  fi
  detalle_del_grupo="$detalle_del_grupo$idioma $bytes B"

  if [ "$ya_contado" -eq 1 ]; then
    detalle_del_grupo="$detalle_del_grupo (ya en el índice)"
    continue
  fi

  if [ "$bytes" -gt "$mayor_del_grupo" ]; then
    mayor_del_grupo=$bytes
    fichero_del_grupo="$relativa"
  fi
done

if [ "$resueltos" -eq 0 ]; then
  fallar "la lista declarada no ha resuelto ni un fichero en '$DIST': la parte nueva de esta medida estaría midiendo la nada."
fi

if [ -n "$fichero_del_grupo" ]; then
  bytes_arranque=$((bytes_arranque + mayor_del_grupo))
  ficheros_arranque+=("$fichero_del_grupo  <- el mayor del grupo de diccionarios: $detalle_del_grupo")
  tamanos_arranque+=("$mayor_del_grupo")
fi

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
