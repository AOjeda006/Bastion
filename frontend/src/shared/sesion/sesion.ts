/**
 * La sesión tal como la ve la aplicación.
 *
 * NO es el DTO del contrato. Los tipos generados no salen de `shared/api` (`stacks/react`), así
 * que aquí se declara el modelo de vista y es `shared/api/sesiones.ts` quien traduce. Un campo que
 * cambie de nombre en el contrato rompe en la traducción, en un sitio, y no en veinte componentes.
 */

/** Una empresa del selector: a las que se pertenece Y en las que se puede operar. */
export interface EmpresaDeLaSesion {
  readonly id: string;
  readonly razonSocial: string;
}

/** Quién está dentro, con qué empresa y con qué permisos. */
export interface Sesion {
  /**
   * Testigo de acceso. **Vive aquí y solo aquí: en memoria.**
   *
   * Nunca en `localStorage` ni en `sessionStorage`: lo que hay ahí lo lee cualquier script de la
   * página, así que un solo XSS se lleva la sesión y sigue sirviendo después de cerrar la pestaña.
   * Lo que sobrevive a una recarga es la cookie `__Host-bastion-refresco`, que es `HttpOnly` y por
   * tanto está fuera del alcance de este código: al arrancar se pide una sesión nueva con ella.
   */
  readonly testigo: string;
  readonly expiraEn: string;
  readonly usuarioId: string;
  readonly nombre: string;
  readonly empresaActivaId: string;
  readonly empresas: readonly EmpresaDeLaSesion[];
  readonly permisos: readonly string[];
}

/** Si la sesión concede un permiso. La interfaz oculta; el servidor autoriza. */
export function concede(sesion: Sesion, permiso: string): boolean {
  return sesion.permisos.includes(permiso);
}

/** La empresa con la que se está operando, o `undefined` si el selector no la trae. */
export function empresaActiva(sesion: Sesion): EmpresaDeLaSesion | undefined {
  return sesion.empresas.find((empresa) => empresa.id === sesion.empresaActivaId);
}
