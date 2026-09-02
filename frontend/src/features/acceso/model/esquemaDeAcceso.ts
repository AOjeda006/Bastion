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
 */
export const esquemaDeAcceso = z.object({
  correo: z
    .string()
    .trim()
    .min(1, 'Escribe tu correo.')
    .max(254, 'El correo no puede pasar de 254 caracteres.')
    .pipe(z.email('Eso no parece un correo electrónico.')),
  contrasena: z
    .string()
    .min(1, 'Escribe tu contraseña.')
    .max(128, 'La contraseña no puede pasar de 128 caracteres.'),
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
