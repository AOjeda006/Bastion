import { useEffect, useState } from 'react';

import { recuperarSesion } from '@/shared/api/cliente.ts';
import { Cargando } from '@/shared/ui/Estados.tsx';

/**
 * Recupera la sesión antes de pintar nada.
 *
 * El testigo de acceso vive en memoria, así que RECARGAR LA PÁGINA LO PIERDE SIEMPRE. Lo que
 * sobrevive es la cookie `__Host-bastion-refresco`, que este código no puede leer porque es
 * `HttpOnly`; lo único que se puede hacer es pedirle al servidor una sesión nueva a cambio de ella.
 *
 * Hasta que esa pregunta se contesta no se sabe si hay sesión, y hay que esperar: pintar el
 * enrutador antes mandaría a la pantalla de acceso a quien ya estaba dentro, y le haría perder la
 * ruta que tenía escrita en la barra de direcciones.
 */
export function ProveedorDeSesion({ children }: { children: React.ReactNode }): React.JSX.Element {
  const [recuperada, setRecuperada] = useState(false);

  useEffect(() => {
    // Un efecto para hablar con un sistema externo —la red—, que es para lo único que sirven.
    // La bandera es su limpieza: si el componente se desmonta antes de que conteste el servidor,
    // no se toca el estado de algo que ya no está.
    let vigente = true;

    void recuperarSesion().finally(() => {
      if (vigente) {
        setRecuperada(true);
      }
    });

    return () => {
      vigente = false;
    };
  }, []);

  if (!recuperada) {
    return <Cargando que="la sesión" />;
  }

  return <>{children}</>;
}
