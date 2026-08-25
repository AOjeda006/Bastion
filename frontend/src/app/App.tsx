/**
 * Raíz de la aplicación.
 *
 * ANDAMIAJE — se sustituye entero en el ítem 0.11 del checklist (`docs/PLAN.md`).
 * Existe solo para que `npm run build` sea verificable desde el primer commit; no se
 * construye encima de él. El 0.11 monta aquí lo que dice el §10 del plan maestro:
 * enrutador de datos, proveedores (caché de servidor, tema, i18n), límites de error,
 * layout y rutas protegidas.
 */
export function App(): React.JSX.Element {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-2">
      <h1 className="text-3xl font-semibold tracking-tight">Bastion</h1>
      <p className="text-sm text-neutral-500">
        Andamiaje del frontal. La aplicación se monta en el ítem 0.11.
      </p>
    </main>
  );
}
