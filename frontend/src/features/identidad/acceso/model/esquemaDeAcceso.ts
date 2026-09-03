import { z } from 'zod';

/**
 * Las reglas del formulario de acceso.
 *
 * Replican las del servidor (`IniciarSesionDto`: correo obligatorio de hasta 254, contraseña
 * obligatoria de hasta 128), no las sustituyen. Esto es comodidad —decirle a alguien que le falta
 * el correo sin ir y volver—; **la autoridad es la API**, que valida otra vez y es la única que
 * sabe si las credenciales son buenas.
 *
 * El tipo del formulario se INFIERE del esquema. Declararlo aparte sería tenerlo escrito dos veces.
 *
 * LOS MENSAJES SON CLAVES, no frases. El esquema es una constante de módulo: se evalúa una sola vez
 * al importarlo, fuera de React y antes de que haya idioma. Una frase escrita aquí quedaría fijada
 * en el idioma de ese instante para toda la vida de la pestaña, y no cambiaría al cambiar de
 * idioma. Guardando la clave, quien traduce es el componente, en cada pintada.
 */
export const esquemaDeAcceso = z.object({
  correo: z
    .string()
    .trim()
    .min(1, 'identidad.acceso.escribeTuCorreo')
    .max(254, 'identidad.acceso.correoDemasiadoLargo')
    .pipe(z.email('identidad.acceso.correoConFormatoMalo')),
  contrasena: z
    .string()
    .min(1, 'identidad.acceso.escribeTuContrasena')
    .max(128, 'identidad.acceso.contrasenaDemasiadoLarga'),
});

export type DatosDeAcceso = z.infer<typeof esquemaDeAcceso>;

/**
 * A dónde volver tras entrar, si la guarda apuntó un destino.
 *
 * El estado de la navegación viene del historial del navegador, que es de fuera: se comprueba antes
 * de usarlo. Solo valen rutas internas —una barra sola al principio—, porque `//evil.example` es
 * una URL absoluta con otro dominio y aceptarla convertiría el acceso en un redirector abierto.
 */
export function destinoSeguro(estado: unknown): string {
  const leido = z.object({ destino: z.string() }).safeParse(estado);

  if (!leido.success) {
    return '/';
  }

  const destino = leido.data.destino;

  return destino.startsWith('/') && !destino.startsWith('//') ? destino : '/';
}
