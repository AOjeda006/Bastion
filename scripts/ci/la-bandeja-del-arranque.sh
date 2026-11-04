#!/usr/bin/env bash
# La bandeja tras el primer arranque: su última migración aplicada, y la fila de la semilla
# publicada, sin empresa y con su correlación (epílogo del 3.4).
#
# POR QUÉ EXISTE. Los tests de integración ejercen el publicador contra una base migrada por ellos.
# Lo desplegado es otra cosa: el migrador del compose aplica las migraciones de Auditoría, la API
# arranca con su publicador, y la semilla da de alta una empresa sin inquilino, lo que deja en la
# cola una fila sin empresa. Que esa fila llegue a publicarse sobre esa base solo se ve aquí. Hasta
# el epílogo del 3.4 esta comprobación vivía en el humo local del agente, que no corre ninguna CI.
#
# QUÉ AFIRMA, cada cosa con lo que la pondría roja:
#   1. la migración LaCausaDeCadaEvento (3.4) está UNA vez en el historial de Auditoría: cero es un
#      migrador que no la aplicó, y dos, un historial roto;
#   2. la fila de la semilla, `organizacion.empresa-creada`, está publicada, sin empresa y con su
#      correlación, y es UNA, porque la semilla da de alta una empresa. Afirmar que la encuentra es
#      lo que hace que un selector con una letra cambiada no salga verde con cero filas;
#   3. ninguna fila de la cola queda sin publicar, y ninguna aparcada.
# El publicador mira la cola cada dos segundos, así que el 2 y el 3 esperan hasta ESPERA segundos a
# que no quede nada pendiente, y después afirman.
#
# DÓNDE CORRE. En el job Humo, después del migrador y antes del segundo arranque: mira el primer
# arranque, sobre la base vacía. En local, como segundo-arranque.sh, contra un proyecto de compose
# APARTE y un fichero de entorno propio:
#   COMPOSE="docker compose -p bastion-humo-XXX --env-file ruta/humo.env -f deploy/docker-compose.yml" \
#   ENTORNO=ruta/humo.env scripts/ci/la-bandeja-del-arranque.sh
#
# No imprime secretos: de la base solo lee recuentos.
set -euo pipefail

read -r -a DC <<< "${COMPOSE:-docker compose -f deploy/docker-compose.yml}"
ENTORNO=${ENTORNO:-deploy/.env}
ESPERA=${ESPERA:-60}

readonly MIGRACION="20261102183738_LaCausaDeCadaEvento"
readonly EVENTO_DE_LA_SEMILLA="organizacion.empresa-creada"

fallar() {
  echo "::error title=La bandeja del arranque::$1"
  exit 1
}

leer_variable() {
  sed -n "s/^$1=//p" "$ENTORNO" | tail -1
}

BASE=$(leer_variable POSTGRES_DB)
USUARIO_DE_LA_BASE=$(leer_variable POSTGRES_USER)

consultar() {
  "${DC[@]}" exec -T postgres psql -U "${USUARIO_DE_LA_BASE:-bastion}" -d "${BASE:-bastion_dev}" \
    -v ON_ERROR_STOP=1 -tAc "$1"
}

# 1. La migración, por su identificador entero: un `LIKE` contaría también una que se llamara
#    parecido. La columna es `migration_id`: la convención de nombres del contexto también renombra
#    las del historial, no solo la tabla.
HISTORIAL=$(consultar "SELECT count(*) FROM auditoria.__historial_de_migraciones
  WHERE migration_id = '${MIGRACION}'")
echo "Historial de Auditoría, con ${MIGRACION}: ${HISTORIAL}."
[ "$HISTORIAL" = "1" ] || fallar "La migración ${MIGRACION} está ${HISTORIAL} veces en el historial de Auditoría, y tiene que estar una."

# 2 y 3. Se espera a que la cola quede vacía de pendientes, y entonces se cuenta.
for INTENTO in $(seq 1 "$ESPERA"); do
  PENDIENTES=$(consultar "SELECT count(*) FROM auditoria.bandeja_de_salida WHERE estado = 'Pendiente'")
  [ "$PENDIENTES" = "0" ] && break
  sleep 1
done
echo "Pendientes en la cola: ${PENDIENTES}, en la mirada ${INTENTO} de ${ESPERA}, una por segundo."

# Una sola consulta, para que las cuatro cifras sean de la misma foto.
read -r TODAS SIN_PUBLICAR APARCADAS DE_LA_SEMILLA <<< "$(consultar "SELECT
    count(*),
    count(*) FILTER (WHERE estado <> 'Publicado'),
    count(*) FILTER (WHERE estado = 'Aparcado'),
    count(*) FILTER (WHERE nombre = '${EVENTO_DE_LA_SEMILLA}' AND estado = 'Publicado'
      AND empresa_id IS NULL AND sin_inquilino IS NOT NULL AND correlacion_id IS NOT NULL)
  FROM auditoria.bandeja_de_salida" | tr '|' ' ')"
echo "Filas de la cola: ${TODAS}; sin publicar: ${SIN_PUBLICAR}; aparcadas: ${APARCADAS}; de la semilla, publicadas, sin empresa y con correlación: ${DE_LA_SEMILLA}."

[ "$DE_LA_SEMILLA" = "1" ] || fallar "La fila de la semilla (${EVENTO_DE_LA_SEMILLA}, publicada, sin empresa, con motivo y con correlación) está ${DE_LA_SEMILLA} veces, y tiene que estar una."
[ "$SIN_PUBLICAR" = "0" ] || fallar "Hay ${SIN_PUBLICAR} sin publicar en la cola, de ellas ${APARCADAS} aparcadas, tras esperar hasta ${ESPERA} s."

echo "::notice title=La bandeja del arranque::${MIGRACION} aplicada, y la fila de la semilla publicada, sin empresa y con su correlación; ninguna pendiente ni aparcada."
