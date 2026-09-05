/**
 * Los permisos que este frontal nombra.
 *
 * Son los MISMOS literales que declara el backend (`PermisosDeOrganizacion`,
 * `PermisosDeIdentidad`). No se generan: el catálogo se publica en tiempo de ejecución —
 * `GET /api/v1/identidad/roles/permisos`— pero no está en el documento OpenAPI como enumerado, así
 * que no hay de dónde generarlo. Es lo único del contrato que aquí se escribe a mano.
 *
 * Si alguno se escribe mal, el fallo es HACIA EL LADO SEGURO: `concede()` devuelve `false`, la
 * opción no se pinta, y quien llegue a la ruta a mano se encuentra con que el servidor deniega
 * igual. La interfaz oculta; el servidor autoriza. Queda anotado como riesgo en `docs/PLAN.md`.
 */
export const PERMISOS = {
  almacenVer: 'organizacion.almacen.ver',
  empresaVer: 'organizacion.empresa.ver',
  terceroVer: 'terceros.tercero.ver',
} as const;
