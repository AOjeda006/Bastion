/**
 * Cierra la ventana en la que está `desde` (`Dialogo`), que es lo que devuelve el foco a quien la
 * abrió. Quien está dentro no la desmonta: la cierra, y `alCerrar` llega con el evento `close`.
 */
export function cerrarElDialogo(desde: Element): void {
  desde.closest('dialog')?.close();
}
