/**
 * El fallo de una llamada a la API, ya traducido a algo que se le puede enseñar a una persona.
 *
 * Se lanza —no se devuelve— porque quien lo consume es la caché de consultas, que distingue el
 * camino feliz del que no lo es por la excepción. El detalle técnico se conserva en `estado` para
 * el registro; lo que se pinta es `message`, que es una frase accionable y nunca un código.
 */
export class FalloDeApi extends Error {
  public readonly estado: number;

  public constructor(estado: number, mensaje: string) {
    super(mensaje);
    this.name = 'FalloDeApi';
    this.estado = estado;
  }
}

/**
 * De código de estado a frase.
 *
 * El 401 no llega aquí casi nunca: el cliente HTTP renueva el testigo y repite la petición. Si
 * llega, es que la renovación tampoco valió, y entonces lo que toca no es un mensaje sino volver a
 * la pantalla de acceso — de eso se encarga la guarda en cuanto la sesión se queda en nada.
 */
export function fallo(estado: number): FalloDeApi {
  if (estado === 403) {
    return new FalloDeApi(
      estado,
      'No tienes permiso para consultar esto con la empresa con la que estás operando.',
    );
  }

  if (estado === 401) {
    return new FalloDeApi(estado, 'Tu sesión ha caducado. Vuelve a entrar.');
  }

  if (estado >= 500) {
    return new FalloDeApi(estado, 'El servidor no ha podido responder. Inténtalo de nuevo.');
  }

  return new FalloDeApi(estado, 'No se han podido cargar los datos. Inténtalo de nuevo.');
}
