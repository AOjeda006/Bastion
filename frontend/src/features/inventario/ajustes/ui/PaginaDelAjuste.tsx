import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';

import { AccionesDelAjuste, type AlAcabar } from './AccionesDelAjuste.tsx';
import { Avisos, type Aviso } from './Avisos.tsx';
import { Estado } from './Estado.tsx';
import { useNombreDelAjuste } from './nombres.ts';
import { useNombre } from '../../maestros/ui/nombres.ts';
import { cantidadLegible } from '../../recuentos/model/cantidad.ts';
import { importeLegible } from '../../saldos/model/saldo.ts';
import { clavesDeAjustes } from '../api/claves.ts';
import { consultarAjuste } from '../api/ajustes.ts';
import type { Ajuste, LineaDeAjuste } from '../model/ajuste.ts';
import { diaLegible } from '@/shared/lib/dias.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';
import { Cargando, Fallo } from '@/shared/ui/Estados.tsx';
import { ExplicacionDeMarca } from '@/shared/ui/Explicacion.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * La ficha de un ajuste: su cabecera, todas sus líneas y lo que se puede hacer con él (ítem 2.17,
 * ADR-0065 §8).
 *
 * **Una ficha por ajuste, con su propio estado**: el aviso de la anulación lleva al inverso, y el
 * inverso, al original, por la misma ruta. Con la clave del identificador, cada uno empieza sin el
 * aviso ni el descarte del otro.
 */
export function PaginaDelAjuste(): React.JSX.Element {
  const { id = '' } = useParams();

  return <Ficha key={id} id={id} />;
}

/**
 * Ver es de su permiso; confirmar, anular y descartar, de los suyos. La interfaz esconde, el
 * servidor autoriza.
 *
 * **Lo que ya se enseñaba no se retira al volver a leerlo.** Tras confirmar o anular, la ficha se
 * lee otra vez; si esa lectura falla, la ficha y el aviso se quedan, con el fallo encima.
 *
 * **Descartado, el ajuste deja de existir** (ADR-0065 §5): la ficha no se vuelve a pedir, porque
 * contestaría un `404`, y queda el aviso con el camino de vuelta al listado. Los avisos están en el
 * mismo sitio del árbol antes y después, para que su región no nazca con el texto dentro.
 */
function Ficha({ id }: { id: string }): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const cache = useQueryClient();
  const [aviso, setAviso] = useState<Aviso | null>(null);
  const [numeroDeAviso, setNumeroDeAviso] = useState(0);
  const [descartado, setDescartado] = useState(false);

  const ficha = useQuery({
    queryKey: clavesDeAjustes.una(id),
    queryFn: () => consultarAjuste(id),
    enabled: !descartado,
  });

  const avisar = (siguiente: Aviso): void => {
    setAviso(siguiente);
    setNumeroDeAviso((numero) => numero + 1);
  };

  // La ficha y los listados, que enseñan su estado y su número. No los nombres: no han cambiado.
  const releer = async (): Promise<void> => {
    await Promise.all([
      cache.invalidateQueries({ queryKey: clavesDeAjustes.una(id) }),
      cache.invalidateQueries({ queryKey: clavesDeAjustes.listas() }),
    ]);
  };

  const acabar: AlAcabar = {
    alConfirmar: async (hecho) => {
      avisar({ clase: 'confirmado', numero: hecho.numero });
      await releer();
    },
    alAnular: async (hecho) => {
      avisar({ clase: 'anulado', inverso: hecho.inverso });
      await releer();
    },
    alDescartar: async () => {
      avisar({ clase: 'descartado' });
      setDescartado(true);
      cache.removeQueries({ queryKey: clavesDeAjustes.una(id) });
      await cache.invalidateQueries({ queryKey: clavesDeAjustes.listas() });
    },
    alFallar: async (error) => {
      avisar({ clase: 'fallo', error });
      await releer();
    },
  };

  const volver = (
    <p className="mt-6 text-sm">
      <Link to="/ajustes" className="underline">
        {t('inventario.ajustes.volver')}
      </Link>
    </p>
  );

  if (ficha.isPending && !descartado) {
    return (
      <>
        <Cargando que={t('inventario.ajustes.ficha.cargando')} />
        {volver}
      </>
    );
  }

  const fallo = ficha.isError ? (
    <Fallo
      mensaje={textoDeFallo(ficha.error)}
      alReintentar={() => {
        void ficha.refetch();
      }}
    />
  ) : null;

  const datos = descartado ? undefined : ficha.data;

  return (
    <>
      {datos !== undefined && <Cabecera ajuste={datos.ajuste} />}

      {/* Con datos, el fallo de volver a leer va encima y no en su lugar. */}
      {fallo}

      <Avisos aviso={aviso} numero={numeroDeAviso} textoDeFallo={textoDeFallo} />

      {datos !== undefined && <Lineas ajuste={datos.ajuste} />}

      {datos !== undefined && (
        <AccionesDelAjuste ficha={datos} ocupada={ficha.isFetching} acabar={acabar} />
      )}

      {volver}
    </>
  );
}

/** El nombre, el estado, el almacén, el día, el motivo, y de dónde viene si viene de algo. */
function Cabecera({ ajuste }: { ajuste: Ajuste }): React.JSX.Element {
  const { t, i18n } = useTranslation();
  const sesion = useSesionAbierta();
  const nombre = useNombreDelAjuste(ajuste);
  const almacen = useNombre('almacen', ajuste.almacenId);
  const porQueElEstado = useId();

  const dato = (termino: string, valor: React.ReactNode, describe?: string): React.JSX.Element => (
    <>
      <dt className="font-medium">{termino}</dt>
      <dd aria-describedby={describe}>{valor}</dd>
    </>
  );

  return (
    <>
      <h2 className="mt-4 text-lg font-semibold">{nombre}</h2>

      <dl className="mt-2 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-sm">
        {dato(
          t('inventario.ajustes.estado'),
          <Estado estado={ajuste.estado} />,
          ajuste.estado === 'desconocido' ? porQueElEstado : undefined,
        )}
        {dato(t('inventario.ajustes.almacen'), almacen.largo)}
        {dato(t('inventario.ajustes.fecha'), diaLegible(ajuste.fechaDeOperacion, i18n.language))}
        {dato(t('inventario.ajustes.motivo'), ajuste.motivo)}
        {ajuste.anulaAId !== null &&
          dato(
            t('inventario.ajustes.ficha.anulaA'),
            <Link to={`/ajustes/${ajuste.anulaAId}`} className="underline">
              {t('inventario.ajustes.ficha.verOriginal')}
            </Link>,
          )}
        {ajuste.recuentoId !== null &&
          dato(
            t('inventario.ajustes.ficha.recuento'),
            concede(sesion, PERMISOS.recuentoVer) ? (
              <Link to={`/recuentos/${ajuste.recuentoId}`} className="underline">
                {t('inventario.ajustes.ficha.verRecuento')}
              </Link>
            ) : (
              t('inventario.ajustes.ficha.deUnRecuento')
            ),
          )}
      </dl>

      {ajuste.estado === 'desconocido' && (
        <ExplicacionDeMarca
          id={porQueElEstado}
          marca={<Estado estado="desconocido" />}
          detalle={t('inventario.ajustes.estados.desconocidoDetalle')}
        />
      )}
    </>
  );
}

/**
 * Todas las líneas, con lo que se escribió y lo que puso el servidor: el factor y la cantidad en
 * unidad base (ADR-0065 §2). El valor es de la confirmación, así que un borrador no lo enseña.
 */
function Lineas({ ajuste }: { ajuste: Ajuste }): React.JSX.Element {
  const { t } = useTranslation();
  const conValor = ajuste.estado !== 'borrador';

  return (
    <>
      <h3 className="mt-6 text-base font-semibold">{t('inventario.ajustes.ficha.lineas')}</h3>

      <p className="mt-1 text-sm text-neutral-700">
        {t('inventario.ajustes.ficha.nota')}
        {!conValor && ` ${t('inventario.ajustes.ficha.notaDelBorrador')}`}
      </p>

      <div className="mt-2 overflow-x-auto">
        <table className="min-w-full border-collapse text-sm">
          <caption className="sr-only">{t('inventario.ajustes.ficha.tabla')}</caption>
          <thead>
            <tr className="border-b border-neutral-300 text-left">
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('inventario.ajustes.ficha.numero')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('inventario.ajustes.ficha.ubicacion')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('inventario.ajustes.ficha.articulo')}
              </th>
              <th scope="col" className="py-2 pr-4 text-right font-medium">
                {t('inventario.ajustes.ficha.cantidad')}
              </th>
              <th scope="col" className="py-2 pr-4 text-right font-medium">
                {t('inventario.ajustes.ficha.factor')}
              </th>
              <th scope="col" className="py-2 pr-4 text-right font-medium">
                {t('inventario.ajustes.ficha.enUnidadBase')}
              </th>
              <th scope="col" className="py-2 pr-4 text-right font-medium">
                {t('inventario.ajustes.ficha.coste')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('inventario.ajustes.ficha.loteOSerie')}
              </th>
              {conValor && (
                <th scope="col" className="py-2 pr-4 text-right font-medium">
                  {t('inventario.ajustes.ficha.valor')}
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {ajuste.lineas.map((linea) => (
              <Fila key={linea.numero} linea={linea} divisa={ajuste.divisa} conValor={conValor} />
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function Fila({
  linea,
  divisa,
  conValor,
}: {
  linea: LineaDeAjuste;
  divisa: string;
  conValor: boolean;
}): React.JSX.Element {
  const { i18n } = useTranslation();
  const ubicacion = useNombre('ubicacion', linea.ubicacionId);
  const articulo = useNombre('articulo', linea.articuloId);
  const unidad = useNombre('unidad', linea.unidadIntroducidaId);
  const idioma = i18n.language;

  const importe = (valor: number | null): string =>
    valor === null ? '' : importeLegible(valor, divisa, idioma);

  return (
    <tr className="border-b border-neutral-200">
      <th scope="row" className="py-2 pr-4 text-left font-normal">
        {linea.numero}
      </th>
      <td className="py-2 pr-4">{ubicacion.corto}</td>
      <td className="py-2 pr-4">{articulo.largo}</td>
      <td className="py-2 pr-4 text-right whitespace-nowrap">
        {`${cantidadLegible(linea.cantidadIntroducida, idioma)} ${unidad.corto}`}
      </td>
      <td className="py-2 pr-4 text-right">{cantidadLegible(linea.factorAUnidadBase, idioma)}</td>
      <td className="py-2 pr-4 text-right">
        {cantidadLegible(linea.cantidadEnUnidadBase, idioma)}
      </td>
      <td className="py-2 pr-4 text-right whitespace-nowrap">{importe(linea.costeUnitario)}</td>
      <td className="py-2 pr-4">{linea.codigoDeLote ?? linea.numeroDeSerie ?? ''}</td>
      {conValor && (
        <td className="py-2 pr-4 text-right whitespace-nowrap">{importe(linea.valor)}</td>
      )}
    </tr>
  );
}
