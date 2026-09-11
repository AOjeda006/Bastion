import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';

import { clavesDeTarifas } from '../api/claves.ts';
import { consultarTarifas } from '../api/consultas.ts';
import { PARAMETRO_DE_BUSQUEDA, PARAMETRO_DE_CODIGO, leerListado } from '../model/listado.ts';
import {
  diaLegible,
  estadoDeVigencia,
  hoyEnElCalendarioLocal,
  type EstadoDeVigencia,
  type Tarifa,
} from '../model/tarifa.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * Listado de tramos de tarifa de la empresa activa, paginado, filtrado por texto y acotable a los
 * tramos de un código.
 *
 * Los tres estados están los tres —cargando, error con salida y vacío con motivo— y los dos filtros
 * viven en la URL, como en el resto de listados.
 *
 * **Lo que esta pantalla tiene que enseñar y las otras no: que una tarifa son VARIAS filas.** El
 * código se repite, una vez por periodo de vigencia, y los periodos no se solapan nunca —lo impide
 * una restricción de exclusión de la base—. Por eso el acotado por código no es un filtro más: es
 * la vista en la que la sucesión de una tarifa se lee entera y se ve si algún día se quedó sin
 * cubrir. Se llega a ella desde la propia fila, igual que al filtro por rama se llega desde el
 * árbol de categorías.
 *
 * **Y no pinta ni precios ni líneas.** Las líneas nombran su destino por identificador —un artículo
 * o una categoría—, y resolver cuarenta nombres serían cuarenta peticiones: exactamente el N+1 que
 * el servidor se niega a hacer para resolver un precio (`ResolverPrecioTests`), y no sale más
 * barato por hacerlo desde el navegador. Un precio, además, sin su divisa al lado es un número que
 * invita a leerse en euros, y la divisa es un maestro de otro módulo que esta funcionalidad no
 * puede nombrar (ADR-0022). Quien necesita un precio lo pide donde se resuelve con su divisa
 * pegada: `GET /tarifas/{codigo}/precio`.
 */
export function PaginaDeTarifas(): React.JSX.Element {
  const { t, i18n } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const [parametros, setParametros] = useSearchParams();
  const listado = leerListado(parametros, leerPaginacion(parametros));

  const consulta = useQuery({
    queryKey: clavesDeTarifas.lista(listado),
    queryFn: () => consultarTarifas(listado),
    // Una tarifa es dato maestro: se abre un tramo y se queda ahí. Minutos, no segundos.
    staleTime: 5 * 60 * 1000,
  });

  // El día de HOY se calcula una vez por pintado y se le pasa a la regla, en vez de que la regla
  // mire el reloj: así el estado de todas las filas se decide con el mismo día —una tabla pintada
  // justo a medianoche no puede salir mitad de un día y mitad del siguiente— y la regla se puede
  // ejercer en la frontera, que es el único sitio donde esto se equivoca.
  const hoy = hoyEnElCalendarioLocal();

  const irA = (pagina: number): void => {
    const siguientes = new URLSearchParams(parametros);
    siguientes.set('pagina', String(pagina));
    setParametros(siguientes);
  };

  // Cambiar un filtro devuelve a la primera página. Quedarse en la séptima enseña una página vacía
  // de un resultado que sí tiene filas, y quien lo ve entiende que no hay ninguna.
  const cambiarFiltro = (cambio: (siguientes: URLSearchParams) => void): void => {
    const siguientes = new URLSearchParams(parametros);
    cambio(siguientes);
    siguientes.delete('pagina');
    setParametros(siguientes);
  };

  const filtrarPor = (texto: string): void => {
    cambiarFiltro((siguientes) => {
      const limpio = texto.trim();

      if (limpio === '') {
        siguientes.delete(PARAMETRO_DE_BUSQUEDA);
      } else {
        siguientes.set(PARAMETRO_DE_BUSQUEDA, limpio);
      }
    });
  };

  const cabecera = (
    <>
      <form
        role="search"
        className="mt-4 flex items-end gap-2"
        onSubmit={(evento) => {
          evento.preventDefault();
          const escrito = new FormData(evento.currentTarget).get(PARAMETRO_DE_BUSQUEDA);

          // `FormData` devuelve texto o fichero, y de un fichero saldría «[object Object]». Aquí no
          // puede haberlo —el campo es un campo de búsqueda— pero eso es una promesa del JSX de
          // abajo, no del tipo, y las promesas de ese tamaño se comprueban.
          filtrarPor(typeof escrito === 'string' ? escrito : '');
        }}
      >
        <label className="flex flex-col gap-1 text-sm">
          {t('catalogo.tarifas.filtro')}
          <input
            type="search"
            name={PARAMETRO_DE_BUSQUEDA}
            defaultValue={listado.busqueda}
            // La clave lleva el filtro dentro: al cambiarlo, el recuadro se vuelve a montar con lo
            // que dice la URL. Sin esto, la flecha de atrás cambiaría la tabla y dejaría escrito el
            // filtro anterior, que es peor que no tener flecha de atrás.
            key={listado.busqueda}
            className="rounded border border-neutral-300 px-2 py-1.5"
          />
        </label>
        <button type="submit" className="rounded border border-neutral-300 px-3 py-1.5 text-sm">
          {t('catalogo.tarifas.filtrar')}
        </button>
      </form>

      {listado.codigo !== null && (
        <p className="mt-3 flex items-center gap-2 text-sm text-neutral-700">
          {/* El rótulo explica la FORMA del dato, no solo que hay un filtro puesto: quien llega
              aquí desde una fila está viendo por primera vez que «PVP» son tres filas, y sin esa
              frase parece que la tarifa esté duplicada. */}
          <span>{t('catalogo.tarifas.tramosDe', { codigo: listado.codigo })}</span>
          <button
            type="button"
            onClick={() => {
              cambiarFiltro((siguientes) => {
                siguientes.delete(PARAMETRO_DE_CODIGO);
              });
            }}
            className="rounded border border-neutral-300 px-2 py-1 text-xs"
          >
            {t('catalogo.tarifas.quitarElCodigo')}
          </button>
        </p>
      )}
    </>
  );

  if (consulta.isPending) {
    return (
      <>
        {cabecera}
        <Cargando que={t('catalogo.tarifas.cargando')} />
      </>
    );
  }

  if (consulta.isError) {
    return (
      <>
        {cabecera}
        <Fallo
          mensaje={textoDeFallo(consulta.error)}
          alReintentar={() => {
            void consulta.refetch();
          }}
        />
      </>
    );
  }

  if (consulta.data.elementos.length === 0) {
    // Qué está vacío, dicho con precisión. El caso del código es el que más se agradece: acotar por
    // un código que no existe devuelve lo mismo que una tarifa recién abierta y todavía sin tramos,
    // y decir «no hay ninguna tarifa» mandaría a darla de alta cuando lo que hay es una errata.
    const vacio =
      listado.codigo !== null
        ? t('catalogo.tarifas.ningunTramoConEseCodigo', { codigo: listado.codigo })
        : listado.busqueda !== ''
          ? t('catalogo.tarifas.ningunaConEsteFiltro', { filtro: listado.busqueda })
          : listado.pagina > 1
            ? t('catalogo.tarifas.paginaVacia')
            : t('catalogo.tarifas.ningunaTodavia');

    return (
      <>
        {cabecera}
        <Vacio mensaje={vacio} />
        <Paginador paginacion={listado} total={consulta.data.total} alCambiar={irA} />
      </>
    );
  }

  return (
    <>
      {cabecera}

      <table className="mt-4 w-full border-collapse text-sm">
        <caption className="sr-only">{t('catalogo.tarifas.tabla')}</caption>
        <thead>
          <tr className="border-b border-neutral-300 text-left">
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('catalogo.tarifas.codigo')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('catalogo.tarifas.nombre')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('catalogo.tarifas.vigencia')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('catalogo.tarifas.estado')}
            </th>
            {/* La columna de la acción no lleva rótulo visible, pero sí nombre: una cabecera vacía
                deja a un lector de pantalla anunciando «columna cinco». */}
            <th scope="col" className="py-2 pr-4 font-medium">
              <span className="sr-only">{t('catalogo.tarifas.acciones')}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {consulta.data.elementos.map((tarifa) => (
            <tr key={tarifa.id} className="border-b border-neutral-200">
              <td className="py-2 pr-4 font-mono">{tarifa.codigo}</td>
              <td className="py-2 pr-4">{tarifa.nombre}</td>
              <td className="py-2 pr-4">
                <Vigencia tarifa={tarifa} idioma={i18n.language} />
              </td>
              <td className="py-2 pr-4">
                <Estado estado={estadoDeVigencia(tarifa, hoy)} />
              </td>
              <td className="py-2 pr-4">
                {listado.codigo === null && (
                  <button
                    type="button"
                    onClick={() => {
                      cambiarFiltro((siguientes) => {
                        siguientes.set(PARAMETRO_DE_CODIGO, tarifa.codigo);
                      });
                    }}
                    className="rounded border border-neutral-300 px-2 py-1 text-xs"
                  >
                    {t('catalogo.tarifas.verSusTramos', { codigo: tarifa.codigo })}
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <Paginador paginacion={listado} total={consulta.data.total} alCambiar={irA} />
    </>
  );
}

/**
 * El periodo de un tramo, con sus dos formas: cerrado por los dos lados, o abierto por el final.
 *
 * Las dos frases enteras en el diccionario, con sus huecos, y no una concatenación de «Del» + fecha
 * + «al» + fecha: el orden de las partes cambia de un idioma a otro, y componer traducciones a
 * trozos es lo que se rompe en cuanto alguien traduce.
 */
function Vigencia({ tarifa, idioma }: { tarifa: Tarifa; idioma: string }): React.JSX.Element {
  const { t } = useTranslation();
  const desde = diaLegible(tarifa.vigenteDesde, idioma);

  return (
    <>
      {tarifa.vigenteHasta === null
        ? t('catalogo.tarifas.desde', { desde })
        : t('catalogo.tarifas.entre', { desde, hasta: diaLegible(tarifa.vigenteHasta, idioma) })}
    </>
  );
}

/**
 * Si el tramo rige hoy, si todavía no, o si ya no.
 *
 * Es un dato y no un color: quien no distinga el verde del gris tiene que poder leerlo igual, así
 * que va escrito y el color solo acompaña. Y el que rige lleva su explicación en el título, porque
 * el caso que confunde es el del tramo que acaba HOY: sigue rigiendo, con el último día incluido.
 */
function Estado({ estado }: { estado: EstadoDeVigencia }): React.JSX.Element {
  const { t } = useTranslation();

  const pinta: Record<EstadoDeVigencia, string> = {
    rige: 'border-emerald-300 bg-emerald-50 text-emerald-900',
    futura: 'border-sky-300 bg-sky-50 text-sky-900',
    caducada: 'border-neutral-300 bg-neutral-50 text-neutral-700',
  };

  const rotulo: Record<EstadoDeVigencia, string> = {
    rige: t('catalogo.tarifas.estados.rige'),
    futura: t('catalogo.tarifas.estados.futura'),
    caducada: t('catalogo.tarifas.estados.caducada'),
  };

  return (
    <span
      title={estado === 'rige' ? t('catalogo.tarifas.estados.rigeDetalle') : undefined}
      className={`rounded border px-1.5 py-0.5 text-xs ${pinta[estado]}`}
    >
      {rotulo[estado]}
    </span>
  );
}
