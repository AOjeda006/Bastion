import type { Fichero } from '@/shared/api/exportacion.ts';

/**
 * Guarda un fichero que ya está en memoria: el navegador lo descarga con su nombre.
 *
 * Con un enlace `download` sobre una URL `blob:`, que es lo único que un navegador acepta como
 * «guardar esto» sin abrir una ventana. El enlace no se queda en la página: se pone, se pulsa y se
 * quita. La URL se revoca en el acto, y no tras un plazo: la especificación de HTML lee el `href`
 * al empezar la descarga, dentro del mismo `click()`, y la de URL ata el fichero a la URL `blob:`
 * al leerla. Revocarla después ya no le quita el fichero a la descarga, y sí devuelve la memoria.
 *
 * @param fichero - Lo que se guarda, con el nombre que le puso el servidor.
 */
export function guardarFichero(fichero: Fichero): void {
  const url = URL.createObjectURL(fichero.contenido);
  const enlace = document.createElement('a');

  enlace.href = url;
  enlace.download = fichero.nombre;
  document.body.append(enlace);
  enlace.click();
  enlace.remove();
  URL.revokeObjectURL(url);
}
