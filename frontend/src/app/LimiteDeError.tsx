import { Component, type ErrorInfo, type ReactNode } from 'react';

import { MensajeDePantallaRota } from './MensajeDePantallaRota.tsx';

/**
 * Límite de error: recoge lo que una pantalla haya roto sin llevarse por delante la aplicación.
 *
 * Va dentro de la disposición y alrededor del `<Outlet>`, con una llave por ruta: así la cabecera,
 * la navegación y el selector de empresa siguen ahí —el usuario puede irse a otra pantalla— y el
 * error no se queda pegado al navegar, que es lo que pasa con un límite que no se reinicia.
 *
 * Al usuario se le da una frase accionable; el detalle técnico va a la consola, que es de donde lo
 * recoge la observabilidad (`principios/manejo-errores.md`).
 */
export class LimiteDeError extends Component<{ children: ReactNode }, { roto: boolean }> {
  public constructor(props: { children: ReactNode }) {
    super(props);
    this.state = { roto: false };
  }

  public static getDerivedStateFromError(): { roto: boolean } {
    return { roto: true };
  }

  public override componentDidCatch(error: Error, informacion: ErrorInfo): void {
    console.error('Pantalla rota:', error, informacion.componentStack);
  }

  public override render(): ReactNode {
    if (!this.state.roto) {
      return this.props.children;
    }

    return <MensajeDePantallaRota />;
  }
}
