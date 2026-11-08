import { useEffect, useId, useState } from 'react';

/**
 * Una ventana modal: el `<dialog>` del navegador, abierto con `showModal()` (ADR-0080).
 *
 * **Nativo, y no una librería ni uno hecho a mano.** `showModal()` pone el resto de la página
 * inerte, así que el foco no sale de la ventana; Escape la cierra, y al cerrarse el foco vuelve a
 * quien la abrió. Es lo que `stacks/react` prohíbe rehacer, y aquí no se rehace: lo hace la
 * plataforma, sin descargar nada.
 *
 * **Se abre al montarse y se desmonta al cerrarse**, con el evento `close` y no antes: cerrar es lo
 * que devuelve el foco, y quitarla de la página sin cerrarla lo tira al principio. Así que para
 * cerrarla se cierra, con `cerrarElDialogo`, y `alCerrar` llega después.
 *
 * **El foco entra por el título**, que no se tabula: lo primero que se lee es qué se pregunta, y lo
 * que avisa va antes de los controles. Lo lleva ahí `showModal()`, que enfoca lo primero que se
 * puede enfocar dentro, y eso es el título por su `tabIndex={-1}`: sin `focus()` (ADR-0081).
 */
export function Dialogo({
  titulo,
  alCerrar,
  children,
}: {
  titulo: string;
  alCerrar: () => void;
  children: React.ReactNode;
}): React.JSX.Element {
  const idDelTitulo = useId();
  // El elemento, en el estado y no en una referencia: abre el efecto que depende de él, y no el del
  // montaje. En desarrollo, el modo estricto monta los efectos dos veces, y uno que abriera al montar
  // cerraría y volvería a abrir; el `close` de ese cierre llega en otra tarea, con la ventana ya
  // abierta otra vez, y la desmontaría. El de aquí, al montar, no tiene elemento y no hace nada.
  const [dialogo, setDialogo] = useState<HTMLDialogElement | null>(null);

  useEffect(() => {
    if (dialogo === null) {
      return;
    }

    dialogo.showModal();

    return () => {
      dialogo.close();
    };
  }, [dialogo]);

  return (
    <dialog
      ref={setDialogo}
      aria-labelledby={idDelTitulo}
      onClose={alCerrar}
      className="m-auto w-[calc(100%-2rem)] max-w-lg rounded border border-neutral-300 bg-white p-6 shadow-lg backdrop:bg-neutral-900/40"
    >
      <h2 id={idDelTitulo} tabIndex={-1} className="text-lg font-semibold">
        {titulo}
      </h2>
      {children}
    </dialog>
  );
}
