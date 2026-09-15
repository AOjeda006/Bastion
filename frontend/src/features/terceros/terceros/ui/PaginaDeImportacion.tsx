import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { clavesDeTerceros } from '../api/claves.ts';
import { importarTerceros } from '../api/importacion.ts';
import {
  CABECERA_DE_LA_PLANTILLA,
  elegir,
  type Eleccion,
  type InformeDeImportacion,
  type MotivoDeRechazo,
} from '../model/importacion.ts';
import { tipoDeFallo } from '@/shared/api/errores.ts';
import { Fallo } from '@/shared/ui/Estados.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * Importar terceros desde un CSV de Excel: la plantilla, el fichero y el informe de lo que no entró.
 *
 * **Lo que se decide aquí, y lo que no.** Esta pantalla no lee el fichero: lo manda entero, en bytes, y
 * lo que es o no es del dialecto lo dice el servidor con un error con nombre (ADR-0034 §5). Adivinar aquí
 * el separador o la codificación sería una segunda opinión que puede no coincidir con la que decide.
 *
 * **La clave de idempotencia es de la elección** (`model/importacion.ts`): repetir el envío del mismo
 * fichero devuelve el mismo informe sin importar dos veces, y elegir otro estrena clave.
 *
 * **Volver a intentarlo solo se ofrece cuando el fallo no tiene nombre** —la red, un 5xx—. Un error con
 * nombre dice qué hay que cambiar en el fichero, y reintentar con el mismo daría el mismo error.
 */
export function PaginaDeImportacion(): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const cache = useQueryClient();
  const idDelFichero = useId();
  const [eleccion, setEleccion] = useState<Eleccion | null>(null);

  const importacion = useMutation({
    mutationFn: importarTerceros,
    onSuccess: async (informe) => {
      // Solo si ha entrado alguno: un informe sin altas no cambia ningún listado.
      if (informe.importadas > 0) {
        await cache.invalidateQueries({ queryKey: clavesDeTerceros.todo });
      }
    },
  });

  const reintentar =
    importacion.isError && tipoDeFallo(importacion.error) === null && eleccion !== null
      ? () => {
          importacion.mutate(eleccion);
        }
      : undefined;

  return (
    <>
      <p className="mt-4 max-w-prose text-sm">{t('terceros.terceros.importacion.explicacion')}</p>

      <h2 className="mt-6 text-base font-medium">{t('terceros.terceros.importacion.plantilla')}</h2>
      <p className="mt-2 max-w-prose text-sm">
        {t('terceros.terceros.importacion.plantillaDetalle')}
      </p>
      <pre className="mt-2 overflow-x-auto rounded border border-neutral-300 bg-neutral-50 p-2 text-xs">
        <code>{CABECERA_DE_LA_PLANTILLA.join(';')}</code>
      </pre>
      <ul className="mt-2 max-w-prose list-disc pl-5 text-sm">
        <li>{t('terceros.terceros.importacion.reglaFormato')}</li>
        <li>{t('terceros.terceros.importacion.reglaValores')}</li>
        <li>{t('terceros.terceros.importacion.reglaTope')}</li>
      </ul>

      <form
        className="mt-6 flex flex-wrap items-end gap-2"
        onSubmit={(evento) => {
          evento.preventDefault();

          if (eleccion !== null) {
            importacion.mutate(eleccion);
          }
        }}
      >
        <label htmlFor={idDelFichero} className="flex flex-col gap-1 text-sm">
          {t('terceros.terceros.importacion.fichero')}
          <input
            id={idDelFichero}
            type="file"
            accept=".csv,text/csv"
            onChange={(evento) => {
              const fichero = evento.currentTarget.files?.[0];

              // Elegir otro fichero es otra operación: clave nueva y el informe anterior fuera, que ya
              // no habla de lo que hay elegido.
              setEleccion(fichero === undefined ? null : elegir(fichero));
              importacion.reset();
            }}
          />
        </label>
        <button
          type="submit"
          disabled={eleccion === null || importacion.isPending}
          className="rounded border border-neutral-300 px-3 py-1.5 text-sm disabled:opacity-50"
        >
          {t('terceros.terceros.importacion.importar')}
        </button>
      </form>

      {importacion.isPending && (
        <p role="status" className="py-4 text-sm text-neutral-500">
          {t('terceros.terceros.importacion.importando')}
        </p>
      )}

      {importacion.isError && (
        <Fallo mensaje={textoDeFallo(importacion.error)} alReintentar={reintentar} />
      )}

      {importacion.isSuccess && <Informe informe={importacion.data} />}
    </>
  );
}

/** El informe: cuántas, y de cada rechazo, dónde y por qué. Nunca el valor que había. */
function Informe({ informe }: { informe: InformeDeImportacion }): React.JSX.Element {
  const { t } = useTranslation();
  const idDelTitulo = useId();

  return (
    <section aria-labelledby={idDelTitulo} className="mt-6">
      <h2 id={idDelTitulo} className="text-base font-medium">
        {t('terceros.terceros.importacion.resultado')}
      </h2>

      <dl className="mt-2 grid max-w-xs grid-cols-2 gap-x-4 text-sm">
        <dt>{t('terceros.terceros.importacion.leidas')}</dt>
        <dd>{informe.leidas}</dd>
        <dt>{t('terceros.terceros.importacion.importadas')}</dt>
        <dd>{informe.importadas}</dd>
        <dt>{t('terceros.terceros.importacion.rechazadas')}</dt>
        <dd>{informe.rechazadas}</dd>
      </dl>

      {informe.leidas === 0 && (
        <p className="mt-2 text-sm">{t('terceros.terceros.importacion.sinFilas')}</p>
      )}

      {informe.leidas > 0 && informe.rechazadas === 0 && (
        <p className="mt-2 text-sm">{t('terceros.terceros.importacion.todasDentro')}</p>
      )}

      {informe.rechazos.length > 0 && (
        <>
          <table className="mt-4 w-full border-collapse text-sm">
            <caption className="text-left font-medium">
              {t('terceros.terceros.importacion.rechazos')}
            </caption>
            <thead>
              <tr className="border-b border-neutral-300 text-left">
                <th scope="col" className="py-2 pr-4 font-medium">
                  {t('terceros.terceros.importacion.columna')}
                </th>
                <th scope="col" className="py-2 pr-4 font-medium">
                  {t('terceros.terceros.importacion.motivo')}
                </th>
                <th scope="col" className="py-2 pr-4 font-medium">
                  {t('terceros.terceros.importacion.lineas')}
                </th>
              </tr>
            </thead>
            <tbody>
              {informe.rechazos.map((rechazo) => (
                <tr
                  key={`${rechazo.columna ?? ''}|${rechazo.motivo}`}
                  className="border-b border-neutral-200"
                >
                  <td className="py-2 pr-4">
                    {rechazo.columna === null ? (
                      t('terceros.terceros.importacion.filaEntera')
                    ) : (
                      <code>{rechazo.columna}</code>
                    )}
                  </td>
                  <td className="py-2 pr-4">
                    <Motivo motivo={rechazo.motivo} />
                  </td>
                  <td className="py-2 pr-4">{rechazo.lineas.join(', ')}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="mt-2 max-w-prose text-sm text-neutral-600">
            {t('terceros.terceros.importacion.lineasDeLaHoja')}
          </p>
        </>
      )}
    </section>
  );
}

/** El motivo, en el idioma en que se está. Una llamada por clave, para que el compilador las compruebe. */
function Motivo({ motivo }: { motivo: MotivoDeRechazo }): React.JSX.Element {
  const { t } = useTranslation();

  switch (motivo) {
    case 'numeroDeCamposDistinto':
      return <>{t('terceros.terceros.importacion.motivos.numeroDeCamposDistinto')}</>;
    case 'comillasMalColocadas':
      return <>{t('terceros.terceros.importacion.motivos.comillasMalColocadas')}</>;
    case 'obligatorio':
      return <>{t('terceros.terceros.importacion.motivos.obligatorio')}</>;
    case 'demasiadoLargo':
      return <>{t('terceros.terceros.importacion.motivos.demasiadoLargo')}</>;
    case 'formatoNoValido':
      return <>{t('terceros.terceros.importacion.motivos.formatoNoValido')}</>;
    case 'noValido':
      return <>{t('terceros.terceros.importacion.motivos.noValido')}</>;
    case 'niClienteNiProveedor':
      return <>{t('terceros.terceros.importacion.motivos.niClienteNiProveedor')}</>;
    case 'yaExiste':
      return <>{t('terceros.terceros.importacion.motivos.yaExiste')}</>;
    case 'repetidaEnElFichero':
      return <>{t('terceros.terceros.importacion.motivos.repetidaEnElFichero')}</>;
    case 'desconocido':
      return <>{t('terceros.terceros.importacion.motivos.desconocido')}</>;
  }
}
