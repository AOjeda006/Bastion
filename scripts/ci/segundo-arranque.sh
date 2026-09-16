#!/usr/bin/env bash
# El segundo arranque: el despliegue vuelve a levantarse sobre una base que ya tiene datos, y el
# sistema tiene que portarse bien también ahí (ítem 1.12, ADR-0035).
#
# POR QUÉ EXISTE. Hasta el 1.12 todo lo que corre al arrancar se había ejercido UNA vez, sobre una
# base vacía: la CI levanta el compose de cero y los tests de integración también. El rol de
# administración nacía con el catálogo entero y nadie miraba qué pasaba cuando el catálogo crecía
# después. Pasaba esto: la semilla no volvía a entrar —ya había usuarios— y el rol se quedaba con
# los permisos de la versión con la que se instaló. Una regresión de privilegios sin un solo error
# en el registro.
#
# QUÉ ESTADO CONSTRUYE, y por qué un estado y no una versión. No se instala una imagen vieja de
# Bastion: se parte del primer arranque de ESTA versión y se deja el rol del sistema como lo habría
# dejado la semilla del cierre de la fase 0 (fe7059d), quitándole lo que aquel catálogo no tenía,
# y se le añade un permiso que este catálogo ya no tiene. Lo primero es lo que se vería al
# actualizar una instalación de la fase 0; lo segundo, al actualizar una cuya versión retiró un
# permiso. Montar la versión vieja costaría otra construcción de imágenes por run y dependería de
# que siguiera construyendo; el estado es lo único que el arranque ve.
#
# QUÉ ES «ARRANQUE». Un arranque de verdad del compose: el migrador y la API se recrean sobre el
# mismo volumen, con las variables de la semilla RETIRADAS —que es como queda una instalación en
# marcha, y el camino en el que la semilla no entra—. No una llamada suelta a un método.
#
# DÓNDE CORRE. Al final del job Humo, sobre el entorno que ese job ya ha levantado: otro job
# tendría que volver a construir las dos imágenes, que es la mayor parte de su tiempo. Y al final
# porque recrea la API, y el frontal resuelve su dirección al arrancar: los pasos del frontal van
# antes.
#
# En local, contra un proyecto de compose APARTE y un fichero de entorno propio:
#   COMPOSE="docker compose -p bastion-humo-XXX --env-file ruta/humo.env -f deploy/docker-compose.yml" \
#   ENTORNO=ruta/humo.env PYTHON=python scripts/ci/segundo-arranque.sh
#
# No imprime secretos: la contraseña viaja de fichero a fichero y el testigo no sale del
# directorio temporal.
set -euo pipefail

read -r -a DC <<< "${COMPOSE:-docker compose -f deploy/docker-compose.yml}"
ENTORNO=${ENTORNO:-deploy/.env}
API=${API:-http://localhost:8080}
PY=${PYTHON:-python3}
TRABAJO=$(mktemp -d)
trap 'rm -rf "$TRABAJO"' EXIT

# El catálogo de permisos con el que cerró la fase 0, tal cual:
#   git show fe7059d:src/Modules/Identidad/Bastion.Identidad.Contracts/PermisosDeIdentidad.cs
#   git show fe7059d:src/Modules/Organizacion/Bastion.Organizacion.Contracts/PermisosDeOrganizacion.cs
# Son 55. Escritos aquí y no calculados porque la CI clona sin historia, y porque son un HECHO del
# pasado: no tienen que seguir al código.
readonly CATALOGO_DE_LA_FASE_0="
identidad.pertenencia.asignar-rol identidad.pertenencia.conceder identidad.pertenencia.retirar
identidad.pertenencia.retirar-rol identidad.pertenencia.ver identidad.rol.crear identidad.rol.modificar
identidad.rol.ver identidad.usuario.bloquear identidad.usuario.cambiar-contrasena identidad.usuario.crear
identidad.usuario.desbloquear identidad.usuario.modificar identidad.usuario.ver
organizacion.almacen.bloquear organizacion.almacen.crear organizacion.almacen.desbloquear
organizacion.almacen.modificar organizacion.almacen.ver organizacion.conversion-um.crear
organizacion.conversion-um.modificar organizacion.conversion-um.ver organizacion.divisa.crear
organizacion.divisa.modificar organizacion.divisa.ver organizacion.ejercicio.cerrar
organizacion.ejercicio.crear organizacion.ejercicio.eliminar organizacion.ejercicio.modificar
organizacion.ejercicio.reabrir organizacion.ejercicio.ver organizacion.empresa.bloquear
organizacion.empresa.crear organizacion.empresa.desbloquear organizacion.empresa.modificar
organizacion.empresa.ver organizacion.impuesto.cerrar organizacion.impuesto.crear
organizacion.impuesto.modificar organizacion.impuesto.ver organizacion.serie.crear
organizacion.serie.eliminar organizacion.serie.modificar organizacion.serie.ver
organizacion.tipo-cambio.crear organizacion.tipo-cambio.modificar organizacion.tipo-cambio.ver
organizacion.ubicacion.bloquear organizacion.ubicacion.crear organizacion.ubicacion.desbloquear
organizacion.ubicacion.modificar organizacion.ubicacion.ver organizacion.unidad-medida.crear
organizacion.unidad-medida.modificar organizacion.unidad-medida.ver
"

# Bien formado y de ningún catálogo: el de una versión que lo tuvo y lo retiró.
readonly PERMISO_RETIRADO="organizacion.permiso-de-una-version-anterior.ver"

# Una lectura que exige un permiso que la fase 0 no tenía.
readonly LECTURA_DE_LA_FASE_1="/api/v1/terceros/terceros"

fallar() {
  echo "::error title=Segundo arranque::$1"
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

# Un inicio de sesión NUEVO cada vez: los permisos viajan en el testigo, así que uno emitido antes
# de cambiar el rol diría lo que el rol era, no lo que es.
iniciar_sesion() {
  CORREO=$(leer_variable BASTION_SEMILLA_ADMIN_CORREO) \
  CONTRASENA=$(leer_variable BASTION_SEMILLA_ADMIN_CONTRASENA) \
    "$PY" -c 'import json, os, sys; json.dump({"correo": os.environ["CORREO"], "contrasena": os.environ["CONTRASENA"]}, sys.stdout)' \
    > "$TRABAJO/credenciales.json"

  local codigo
  codigo=$(curl --silent --output "$TRABAJO/sesion.json" --write-out '%{http_code}' \
    --request POST "$API/api/v1/identidad/sesiones" \
    --header 'Content-Type: application/json' --data-binary "@$TRABAJO/credenciales.json")
  rm -f "$TRABAJO/credenciales.json"

  [ "$codigo" = "200" ] || fallar "La cuenta sembrada no ha podido iniciar sesión ($1): HTTP $codigo."

  "$PY" -c 'import json, sys; print(json.load(open(sys.argv[1], encoding="utf-8"))["tokenDeAcceso"], end="")' \
    "$TRABAJO/sesion.json" > "$TRABAJO/testigo"
  rm -f "$TRABAJO/sesion.json"
}

pedir() {
  curl --silent --output "$2" --write-out '%{http_code}' \
    --header "Authorization: Bearer $(cat "$TRABAJO/testigo")" "$API$1"
}

# El rol del sistema contra el catálogo que sirve la API, entero y en los dos sentidos. Lo que
# falta es la regresión de privilegios; lo que sobra, un permiso que ya no existe y que aparecería
# concedido el día que alguien volviera a registrar ese nombre.
comparar_con_el_catalogo() {
  local codigo
  codigo=$(pedir /api/v1/identidad/roles/permisos "$TRABAJO/catalogo.json")
  [ "$codigo" = "200" ] || fallar "El catálogo de permisos no se lee ($1): HTTP $codigo."
  codigo=$(pedir "/api/v1/identidad/roles?size=100" "$TRABAJO/roles.json")
  [ "$codigo" = "200" ] || fallar "Los roles no se leen ($1): HTTP $codigo."

  "$PY" - "$TRABAJO/catalogo.json" "$TRABAJO/roles.json" "$1" <<'PYTHON'
import json, sys

catalogo = set(json.load(open(sys.argv[1], encoding="utf-8")))
roles = [r for r in json.load(open(sys.argv[2], encoding="utf-8"))["elementos"] if r["esDelSistema"]]
momento = sys.argv[3]

if not catalogo:
    print(f"::error title=Segundo arranque::El catálogo servido está vacío ({momento}): no hay nada con que comparar.")
    sys.exit(1)
if not roles:
    print(f"::error title=Segundo arranque::No hay ningún rol del sistema ({momento}).")
    sys.exit(1)

mal = False
for rol in roles:
    concedidos = set(rol["permisos"])
    faltan = sorted(catalogo - concedidos)
    sobran = sorted(concedidos - catalogo)
    if faltan or sobran:
        mal = True
        print(f"::error title=Segundo arranque::{momento}, el rol del sistema «{rol['codigo']}» no es el catálogo: "
              f"le faltan {len(faltan)} {faltan} y le sobran {len(sobran)} {sobran}.")
    else:
        print(f"::notice title=Segundo arranque::{momento}, el rol del sistema «{rol['codigo']}» concede el catálogo entero: {len(catalogo)} permisos.")
sys.exit(1 if mal else 0)
PYTHON
}

# ------------------------------------------------------------------------------ primer arranque
# El camino de siempre, afirmado: si esto no se cumple, lo roto es el primer arranque y lo que
# venga después no significa nada.
iniciar_sesion "tras el primer arranque"
comparar_con_el_catalogo "Tras el primer arranque"

# Y en ese primer arranque el migrador no tenía rol que alinear —corre antes que la API, y el rol
# lo crea la semilla—, y lo dijo. Sin esta línea, un migrador que creara el rol por su cuenta
# dejaría el mismo verde con la semilla sin ejercer.
"${DC[@]}" logs --no-color migraciones | grep -q '"SinRolesDelSistema"' \
  || fallar "El migrador del primer arranque no dice que aún no hay rol del sistema (suceso SinRolesDelSistema)."

# --------------------------------------------------------------------------------- estado viejo
LISTA=$(printf "'%s'," $CATALOGO_DE_LA_FASE_0)
consultar "
  DELETE FROM identidad.permisos_de_rol AS p USING identidad.roles AS r
   WHERE p.rol_id = r.id AND r.es_del_sistema AND p.permiso <> ALL (ARRAY[${LISTA%,}]);
  INSERT INTO identidad.permisos_de_rol (rol_id, permiso)
  SELECT id, '$PERMISO_RETIRADO' FROM identidad.roles WHERE es_del_sistema;" > /dev/null

TIENE=$(consultar "SELECT count(*) FROM identidad.permisos_de_rol AS p JOIN identidad.roles AS r ON r.id = p.rol_id WHERE r.es_del_sistema")
ESPERADOS=$(( $(echo $CATALOGO_DE_LA_FASE_0 | wc -w) + 1 ))
[ "$TIENE" = "$ESPERADOS" ] \
  || fallar "El estado viejo no se ha construido: el rol del sistema tiene $TIENE permisos y tenía que tener $ESPERADOS."

# El arnés del arnés: con el estado viejo, una lectura de la fase 1 se deniega. Si no, el estado no
# es viejo —o la autorización no mira el rol— y un verde de abajo no probaría nada.
iniciar_sesion "con el estado viejo"
CODIGO=$(pedir "$LECTURA_DE_LA_FASE_1" "$TRABAJO/lectura.json")
[ "$CODIGO" = "403" ] \
  || fallar "Con el rol de la fase 0, GET $LECTURA_DE_LA_FASE_1 tenía que ser 403 y es $CODIGO: el estado viejo no es viejo."
echo "::notice title=Segundo arranque::Estado viejo construido: el rol del sistema con los $((ESPERADOS - 1)) permisos de la fase 0 y uno retirado; GET $LECTURA_DE_LA_FASE_1 -> 403."

# ------------------------------------------------------------------------------ segundo arranque
# Las ocho variables de la semilla, vacías: pisan las del fichero de entorno, y el compose las
# interpola con `:-`. Es la instalación en marcha que las retiró, como recomienda el ejemplo.
BASTION_SEMILLA_ADMIN_CORREO= BASTION_SEMILLA_ADMIN_CONTRASENA= BASTION_SEMILLA_EMPRESA_NIF= \
BASTION_SEMILLA_EMPRESA_RAZON_SOCIAL= BASTION_SEMILLA_EMPRESA_CALLE= \
BASTION_SEMILLA_EMPRESA_CODIGO_POSTAL= BASTION_SEMILLA_EMPRESA_POBLACION= BASTION_SEMILLA_EMPRESA_PAIS= \
  "${DC[@]}" up --detach --wait --wait-timeout 300 --no-deps --force-recreate migraciones api

MIGRADOR=$("${DC[@]}" ps --all --quiet migraciones | head -1)
[ -n "$MIGRADOR" ] || fallar "No existe el contenedor del migrador tras el segundo arranque."
SALIDA=$(docker inspect --format '{{.State.ExitCode}}' "$MIGRADOR")
[ "$SALIDA" = "0" ] || fallar "El migrador del segundo arranque ha salido con código $SALIDA."
"${DC[@]}" logs --no-color migraciones > "$TRABAJO/migraciones.log"
grep -E 'RolDelSistema' "$TRABAJO/migraciones.log" || true

# Que el rol vuelva a estar entero tiene que ser obra del migrador y decirlo él, con un suceso por
# permiso: un verde que llegara por otro camino —una semilla que entrara, otro proceso— no probaría
# el arreglo. Lo retirado es uno y se cuenta; lo concedido depende del catálogo y lo afirma abajo la
# comparación con la API.
grep -q '"RolDelSistemaAlineado"' "$TRABAJO/migraciones.log" \
  || fallar "El migrador del segundo arranque no dice haber alineado el rol del sistema (suceso RolDelSistemaAlineado)."
RETIRADOS=$(grep -c '"PermisoRetiradoDelRolDelSistema"' "$TRABAJO/migraciones.log" || true)
[ "$RETIRADOS" = "1" ] \
  || fallar "El migrador del segundo arranque dice haber retirado $RETIRADOS permisos del rol del sistema, y era uno: $PERMISO_RETIRADO."

# Que la semilla NO entró es parte del estado que se afirma: si entrara, esto sería otro primer
# arranque con más pasos.
"${DC[@]}" logs --no-color api | grep -q 'SemillaSinVariables' \
  || fallar "La API del segundo arranque no dice que la semilla se queda fuera: las variables no se han retirado."

iniciar_sesion "tras el segundo arranque"
comparar_con_el_catalogo "Tras el segundo arranque"

CODIGO=$(pedir "$LECTURA_DE_LA_FASE_1" "$TRABAJO/lectura.json")
[ "$CODIGO" = "200" ] \
  || fallar "Tras el segundo arranque, GET $LECTURA_DE_LA_FASE_1 es $CODIGO: la administración sigue sin los permisos de la fase 1."
echo "::notice title=Segundo arranque::Tras el segundo arranque, con la semilla fuera, GET $LECTURA_DE_LA_FASE_1 -> 200."
