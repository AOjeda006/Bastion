import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';

import { clavesDeTerceros } from '../api/claves.ts';
import { consultarTerceros } from '../api/consultas.ts';
import { PARAMETRO_DE_BUSQUEDA, leerListado } from '../model/listado.ts';
import type { Tercero, Verificacion } from '../model/tercero.ts';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';
import { leerPaginacion } from '@/shared/lib/parametrosDeUrl.ts';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { Paginador } from '@/shared/ui/Paginacion.tsx';

/**
 * Listado de terceros de la empresa activa, paginado y filtrado.
 *
 * Los tres estados están los tres —cargando, error con salida y vacío con motivo—, y tanto la
 * página como el filtro viven en la URL: los datos están en la caché de consultas y no se copian a
 * ningún estado local, que sería una segunda verdad que envejece justo al cambiar de empresa.
 *
 * **El recuadro de filtro no busca por identificador fiscal, y eso es una decisión** (ADR-0025).
 * Lo que se teclea aquí acaba en la barra de direcciones, o sea en el historial del navegador, en
 * el enlace que se copia por chat y en el registro de acceso del servidor de delante. Un trozo de
 * nombre comercial es lo que se lee en una pantalla; el NIF de un cliente es muy a menudo el DNI
 * de una persona física, y buscar por él va por el cuerpo con `POST .../buscar`. Si alguien añade
 * aquí un campo «NIF», lo que hay que cambiar es por dónde viaja, no este comentario.
 */
export function PaginaDeTerceros(): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const [parametros, setParametros] = useSearchParams();
  const listado = leerListado(parametros, leerPaginacion(parametros));

  const consulta = useQuery({
    queryKey: clavesDeTerceros.lista(listado),
    queryFn: () => consultarTerceros(listado),
    // Un tercero es dato maestro: se da de alta y se queda ahí. Minutos, no segundos.
    staleTime: 5 * 60 * 1000,
  });

  const irA = (pagina: number): void => {
    const siguientes = new URLSearchParams(parametros);
    siguientes.set('pagina', String(pagina));
    setParametros(siguientes);
  };

  // Filtrar devuelve a la primera página, y no es un detalle: quedarse en la séptima al cambiar el
  // filtro enseña una página vacía de un resultado que sí tiene filas, y quien lo ve entiende que
  // no hay ninguno.
  const filtrarPor = (texto: string): void => {
    const siguientes = new URLSearchParams(parametros);
    const limpio = texto.trim();

    if (limpio === '') {
      siguientes.delete(PARAMETRO_DE_BUSQUEDA);
    } else {
      siguientes.set(PARAMETRO_DE_BUSQUEDA, limpio);
    }

    siguientes.delete('pagina');
    setParametros(siguientes);
  };

  const buscador = (
    <form
      role="search"
      className="mt-4 flex items-end gap-2"
      onSubmit={(evento) => {
        evento.preventDefault();
        const escrito = new FormData(evento.currentTarget).get(PARAMETRO_DE_BUSQUEDA);

        // `FormData` devuelve texto o fichero, y de un fichero saldría «[object Object]». Aquí no
        // puede haberlo —el campo es un `<input type="search">`— pero eso es una promesa del JSX de
        // arriba, no del tipo, y las promesas de ese tamaño se comprueban en vez de suponerse.
        filtrarPor(typeof escrito === 'string' ? escrito : '');
      }}
    >
      <label className="flex flex-col gap-1 text-sm">
        {t('terceros.terceros.filtro')}
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
        {t('terceros.terceros.filtrar')}
      </button>
    </form>
  );

  if (consulta.isPending) {
    return (
      <>
        {buscador}
        <Cargando que={t('terceros.terceros.cargando')} />
      </>
    );
  }

  if (consulta.isError) {
    return (
      <>
        {buscador}
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
    // Qué está vacío, dicho con precisión: no es lo mismo «no hay terceros» que «no hay ninguno
    // que case con lo que has escrito», y confundirlos manda a dar de alta algo que ya existe.
    const vacio =
      listado.busqueda !== ''
        ? t('terceros.terceros.ningunoConEsteFiltro', { filtro: listado.busqueda })
        : listado.pagina > 1
          ? t('terceros.terceros.paginaVacia')
          : t('terceros.terceros.ningunoTodavia');

    return (
      <>
        {buscador}
        <Vacio mensaje={vacio} />
        <Paginador paginacion={listado} total={consulta.data.total} alCambiar={irA} />
      </>
    );
  }

  return (
    <>
      {buscador}

      <table className="mt-4 w-full border-collapse text-sm">
        <caption className="sr-only">{t('terceros.terceros.tabla')}</caption>
        <thead>
          <tr className="border-b border-neutral-300 text-left">
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('terceros.terceros.identificador')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('terceros.terceros.razonSocial')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('terceros.terceros.poblacion')}
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              {t('terceros.terceros.papel')}
            </th>
          </tr>
        </thead>
        <tbody>
          {consulta.data.elementos.map((tercero) => (
            <tr key={tercero.id} className="border-b border-neutral-200">
              <td className="py-2 pr-4">
                <span className="font-mono">
                  {tercero.pais} {tercero.numero}
                </span>{' '}
                <Sello verificacion={tercero.verificacion} />
              </td>
              <td className="py-2 pr-4">
                {tercero.razonSocial}
                {tercero.nombreComercial !== null && (
                  <span className="block text-neutral-600">{tercero.nombreComercial}</span>
                )}
              </td>
              <td className="py-2 pr-4 text-neutral-600">{tercero.poblacion}</td>
              <td className="py-2 pr-4">
                <Papel tercero={tercero} />
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
 * Si el identificador está comprobado, y qué significa que no lo esté.
 *
 * **Se pinta siempre, también cuando está verificado.** Enseñarlo solo cuando algo va mal deja a
 * quien mira sin saber si la ausencia de sello quiere decir «comprobado» o «esta versión todavía
 * no lo enseñaba», y esa duda vale lo mismo que no decir nada. El título explica la consecuencia,
 * que es lo que hace falta para decidir: un identificador sin comprobar puede estar mal tecleado y
 * acaba impreso en una factura.
 *
 * `desconocida` no es defensa por si acaso: el enumerado viaja como texto y el frontal se despliega
 * aparte del backend, así que un valor nuevo llega antes de que este fichero lo conozca.
 */
function Sello({ verificacion }: { verificacion: Verificacion }): React.JSX.Element {
  const { t } = useTranslation();

  const verificado = verificacion === 'verificado';

  const texto = verificado
    ? t('terceros.terceros.verificacion.verificado')
    : verificacion === 'sinVerificar'
      ? t('terceros.terceros.verificacion.sinVerificar')
      : t('terceros.terceros.verificacion.desconocida');

  const detalle = verificado
    ? t('terceros.terceros.verificacion.verificadoDetalle')
    : verificacion === 'sinVerificar'
      ? t('terceros.terceros.verificacion.sinVerificarDetalle')
      : t('terceros.terceros.verificacion.desconocidaDetalle');

  return (
    <span
      title={detalle}
      className={
        'rounded border px-1.5 py-0.5 text-xs ' +
        (verificado
          ? 'border-emerald-300 bg-emerald-50 text-emerald-900'
          : 'border-amber-300 bg-amber-50 text-amber-900')
      }
    >
      {texto}
    </span>
  );
}

/** Cliente, proveedor o las dos cosas. Ninguna de las dos no existe: lo impide el dominio. */
function Papel({ tercero }: { tercero: Tercero }): React.JSX.Element {
  const { t } = useTranslation();

  if (tercero.esCliente && tercero.esProveedor) {
    return <>{t('terceros.terceros.papeles.ambos')}</>;
  }

  return (
    <>
      {tercero.esCliente
        ? t('terceros.terceros.papeles.cliente')
        : t('terceros.terceros.papeles.proveedor')}
    </>
  );
}
