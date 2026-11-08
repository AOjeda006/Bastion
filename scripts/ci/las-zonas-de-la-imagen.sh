#!/usr/bin/env bash
# Las tres zonas admitidas, resueltas en la imagen de la API (ítem 3.5, ADR-0079 §11).
#
# POR QUÉ EXISTE. El día de negocio sale de la zona de su almacén o de su empresa, y .NET resuelve
# cada zona con los ficheros de `tzdata` de la imagen en la que corre. Una imagen sin ellos —una
# *chiseled* sin el sufijo `-extra`— arranca igual, pasa las sondas, y contesta un 500 en cuanto
# alguien pide un día. Mirar que los ficheros estén no basta: lo que importa es lo que .NET hace
# con ellos. Así que se pregunta a la API desplegada qué día es en cada zona y cuándo acaba, y se
# comprueba que cada final sea la medianoche de su zona.
#
# QUÉ AFIRMA, de `GET /api/v1/organizacion/hoy`, cada cosa con lo que la pondría roja:
#   1. un 200, con las tres zonas admitidas y ninguna más: una zona que .NET no resuelve es un 500;
#   2. el día de cada una acaba a la medianoche de su zona, en horas enteras: Madrid y Ceuta, a +1
#      o a +2, y Canarias, a 0 o a +1. Una zona que se resolviera como UTC acabaría a 0 en todas;
#   3. Ceuta va con Madrid, y Canarias, una hora por detrás: con el mismo día, su medianoche llega
#      una hora después, y si no es el mismo, Madrid va un día por delante. Sin esto, Canarias
#      resuelta con las reglas de Madrid pasaría el 2 cada invierno;
#   4. el día de la empresa sembrada, que está en Madrid, es el de Madrid: une las zonas a la
#      empresa, que es por donde las lee todo lo demás.
#
# DÓNDE CORRE. En el job Humo, después del paso que inicia sesión con la cuenta sembrada, con su
# testigo. En local, contra un proyecto de compose APARTE, con un testigo de esa cuenta en un
# fichero:
#   API=http://localhost:45080 TESTIGO=ruta/testigo PYTHON=python \
#     bash scripts/ci/las-zonas-de-la-imagen.sh
#
# No imprime secretos: el testigo se lee de su fichero, y la respuesta va a un directorio temporal.
set -euo pipefail

API=${API:-http://localhost:8080}
TESTIGO=${TESTIGO:-testigo.txt}
PY=${PYTHON:-python3}
TRABAJO=$(mktemp -d)
trap 'rm -rf "$TRABAJO"' EXIT

fallar() {
  echo "::error title=Las zonas de la imagen::$1"
  exit 1
}

[ -s "$TESTIGO" ] || fallar "No hay testigo en «$TESTIGO»: este paso va después de iniciar sesión."

CODIGO=$(curl --silent --output "$TRABAJO/hoy.json" --write-out '%{http_code}' \
  --header "Authorization: Bearer $(cat "$TESTIGO")" "$API/api/v1/organizacion/hoy")
echo "GET /api/v1/organizacion/hoy -> $CODIGO"
[ "$CODIGO" = "200" ] ||
  fallar "La API no ha dicho qué día es (HTTP $CODIGO). Con una zona que .NET no resuelve, es un 500."

# `-X utf8`, para que los mensajes salgan en UTF-8 también en Windows, donde la salida de python a
# una tubería va en la página de códigos del sistema.
"$PY" -X utf8 - "$TRABAJO/hoy.json" <<'COMPROBAR'
import json
import sys
from datetime import date, datetime, timedelta, timezone

# Las admitidas, con el desplazamiento de su invierno y el de su verano.
ADMITIDAS = {"Europe/Madrid": (1, 2), "Atlantic/Canary": (0, 1), "Africa/Ceuta": (1, 2)}


def fallar(texto):
    print(f"::error title=Las zonas de la imagen::{texto}")
    sys.exit(1)


with open(sys.argv[1], encoding="utf-8") as fichero:
    hoy = json.load(fichero)

zonas = {}
for zona in hoy.get("enCadaZona") or []:
    dia = date.fromisoformat(zona["dia"])
    acaba = datetime.fromisoformat(zona["acabaEn"]).astimezone(timezone.utc)
    # La medianoche del día siguiente, leída como si fuera UTC, menos el instante en que acaba: lo
    # que esa zona va por delante de UTC esa noche.
    medianoche = datetime.combine(dia + timedelta(days=1), datetime.min.time(), timezone.utc)
    zonas[zona["zona"]] = (dia, acaba, (medianoche - acaba) / timedelta(hours=1))

if sorted(zonas) != sorted(ADMITIDAS):
    fallar(f"La API publica {sorted(zonas)}, y las admitidas son {sorted(ADMITIDAS)}.")

for nombre, (dia, acaba, horas) in zonas.items():
    print(f"{nombre}: hoy es {dia}, y acaba en {acaba:%Y-%m-%dT%H:%MZ}, a UTC{horas:+g}")
    if horas not in ADMITIDAS[nombre]:
        invierno, verano = ADMITIDAS[nombre]
        fallar(f"El día de {nombre} acaba a UTC{horas:+g}, y en esa zona es a UTC{invierno:+d} o a "
               f"UTC{verano:+d}: .NET no la resuelve con sus reglas.")

madrid, canarias, ceuta = (zonas[nombre] for nombre in ADMITIDAS)
if (ceuta[0], ceuta[2]) != (madrid[0], madrid[2]):
    fallar(f"Ceuta va el {ceuta[0]} a UTC{ceuta[2]:+g}, y Madrid el {madrid[0]} a UTC{madrid[2]:+g}.")
if canarias[0] == madrid[0] and madrid[2] - canarias[2] != 1:
    fallar(f"Con el mismo día, Canarias va a UTC{canarias[2]:+g} y Madrid a UTC{madrid[2]:+g}: "
           "no le lleva una hora.")
if canarias[0] != madrid[0] and madrid[0] != canarias[0] + timedelta(days=1):
    fallar(f"Canarias va el {canarias[0]} y Madrid el {madrid[0]}: Madrid no va un día por delante.")
if hoy.get("deLaEmpresa") != madrid[0].isoformat():
    fallar(f"El día de la empresa sembrada es {hoy.get('deLaEmpresa')}, y su zona, Madrid, va el "
           f"{madrid[0]}.")

print("::notice title=Las zonas de la imagen::La imagen resuelve las tres: "
      + "; ".join(f"{nombre}, UTC{horas:+g}" for nombre, (_, _, horas) in zonas.items()) + ".")
COMPROBAR
