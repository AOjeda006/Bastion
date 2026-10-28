import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';

import { AccionesDeLaTransferencia, type AlAcabar } from './AccionesDeLaTransferencia.tsx';
import { Avisos, type Aviso } from './Avisos.tsx';
import { Estado } from './Estado.tsx';
import { useNombreDeLaTransferencia } from './nombres.ts';
import { useNombre } from '../../maestros/ui/nombres.ts';
import { cantidadLegible } from '../../recuentos/model/cantidad.ts';
import { importeLegible } from '../../saldos/model/saldo.ts';
import { clavesDeTransferencias } from '../api/claves.ts';
import { consultarTransferencia, exportarLineas } from '../api/transferencias.ts';
import type { LineaDeTransferencia, Transferencia } from '../model/transferencia.ts';
import { diaLegible } from '@/shared/lib/dias.ts';
import { BotonDeExportar } from '@/shared/ui/BotonDeExportar.tsx';
import { Cargando, Fallo } from '@/shared/ui/Estados.tsx';
import { ExplicacionDeMarca } from '@/shared/ui/Explicacion.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/**
 * La ficha de una transferencia: su cabecera, todas sus líneas y lo que se puede hacer con ella
 * (ítem 2.19, ADR-0068 §8).
 *
 * **Una ficha por transferencia, con su propio estado**: el aviso de la anulación lleva al inverso,
 * y el inverso, a la original, por la misma ruta. Con la clave del identificador, cada una empieza
 * sin el aviso ni el descarte de la otra.
 */
export function PaginaDeLaTransferencia(): React.JSX.Element {
  const { id = '' } = useParams();

  return <Ficha key={id} id={id} />;
}

/**
 * Ver es de su permiso; enviar, recibir, anular y descartar, de los suyos. La interfaz esconde, el
 * servidor autoriza.
 *
 * **Lo que ya se enseñaba no se retira al volver a leerlo.** Tras cada acción, la ficha se lee otra
 * vez; si esa lectura falla, la ficha y el aviso se quedan, con el fallo encima.
 *
 * **Descartada, la transferencia deja de existir** (ADR-0068 §5): la ficha no se vuelve a pedir,
 * porque contestaría un `404`, y queda el aviso con el camino de vuelta al listado.
 */
function Ficha({ id }: { id: string }): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const cache = useQueryClient();
  const [aviso, setAviso] = useState<Aviso | null>(null);
  const [numeroDeAviso, setNumeroDeAviso] = useState(0);
  const [descartada, setDescartada] = useState(false);

  const ficha = useQuery({
    queryKey: clavesDeTransferencias.una(id),
    queryFn: () => consultarTransferencia(id),
    enabled: !descartada,
  });

  const avisar = (siguiente: Aviso): void => {
    setAviso(siguiente);
    setNumeroDeAviso((numero) => numero + 1);
  };

  // La ficha y los listados, que enseñan su estado, su número y sus fechas. No los nombres: no han
  // cambiado.
  const releer = async (): Promise<void> => {
    await Promise.all([
      cache.invalidateQueries({ queryKey: clavesDeTransferencias.una(id) }),
      cache.invalidateQueries({ queryKey: clavesDeTransferencias.listas() }),
    ]);
  };

  const acabar: AlAcabar = {
    alEnviar: async (hecho) => {
      avisar({ clase: 'enviada', numero: hecho.numero });
      await releer();
    },
    alRecibir: async (hecho, dia) => {
      avisar({ clase: 'recibida', dia: hecho.fechaDeRecepcion ?? dia });
      await releer();
    },
    alAnular: async (hecho) => {
      avisar({ clase: 'anulada', inverso: hecho.inverso });
      await releer();
    },
    alDescartar: async () => {
      avisar({ clase: 'descartada' });
      setDescartada(true);
      cache.removeQueries({ queryKey: clavesDeTransferencias.una(id) });
      await cache.invalidateQueries({ queryKey: clavesDeTransferencias.listas() });
    },
    alFallar: async (error) => {
      avisar({ clase: 'fallo', error });
      await releer();
    },
  };

  const volver = (
    <p className="mt-6 text-sm">
      <Link to="/transferencias" className="underline">
        {t('inventario.transferencias.volver')}
      </Link>
    </p>
  );

  if (ficha.isPending && !descartada) {
    return (
      <>
        <Cargando que={t('inventario.transferencias.ficha.cargando')} />
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

  const datos = descartada ? undefined : ficha.data;

  return (
    <>
      {datos !== undefined && <Cabecera transferencia={datos.transferencia} />}

      {/* Con datos, el fallo de volver a leer va encima y no en su lugar. */}
      {fallo}

      <Avisos aviso={aviso} numero={numeroDeAviso} textoDeFallo={textoDeFallo} />

      {datos !== undefined && <Lineas transferencia={datos.transferencia} />}

      {datos !== undefined && (
        <AccionesDeLaTransferencia ficha={datos} ocupada={ficha.isFetching} acabar={acabar} />
      )}

      {volver}
    </>
  );
}

/** El nombre, el estado, los dos almacenes, las dos fechas, y lo que compensa si es un inverso. */
function Cabecera({ transferencia }: { transferencia: Transferencia }): React.JSX.Element {
  const { t, i18n } = useTranslation();
  const nombre = useNombreDeLaTransferencia(transferencia);
  const origen = useNombre('almacen', transferencia.almacenOrigenId);
  const destino = useNombre('almacen', transferencia.almacenDestinoId);
  const porQueElEstado = useId();
  const dia = (valor: string): string => diaLegible(valor, i18n.language);

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
          t('inventario.transferencias.estado'),
          <Estado estado={transferencia.estado} />,
          transferencia.estado === 'desconocido' ? porQueElEstado : undefined,
        )}
        {dato(t('inventario.transferencias.origen'), origen.largo)}
        {dato(t('inventario.transferencias.destino'), destino.largo)}
        {dato(t('inventario.transferencias.envio'), dia(transferencia.fechaDeEnvio))}
        {transferencia.fechaDeRecepcion !== null &&
          dato(t('inventario.transferencias.recepcion'), dia(transferencia.fechaDeRecepcion))}
        {transferencia.motivo !== null &&
          dato(t('inventario.transferencias.motivo'), transferencia.motivo)}
        {transferencia.anulaAId !== null &&
          dato(
            t('inventario.transferencias.ficha.anulaA'),
            <Link to={`/transferencias/${transferencia.anulaAId}`} className="underline">
              {t('inventario.transferencias.ficha.verOriginal')}
            </Link>,
          )}
      </dl>

      {transferencia.estado === 'desconocido' && (
        <ExplicacionDeMarca
          id={porQueElEstado}
          marca={<Estado estado="desconocido" />}
          detalle={t('inventario.transferencias.estados.desconocidoDetalle')}
        />
      )}
    </>
  );
}

/**
 * Todas las líneas, con lo que se escribió y lo que puso el servidor: el factor y la cantidad en
 * unidad base (ADR-0068 §2). El valor es del envío, así que un borrador no lo enseña.
 */
function Lineas({ transferencia }: { transferencia: Transferencia }): React.JSX.Element {
  const { t } = useTranslation();
  const conValor = transferencia.estado !== 'borrador';

  const columna = (texto: string, aLaDerecha = false): React.JSX.Element => (
    <th scope="col" className={`py-2 pr-4 font-medium ${aLaDerecha ? 'text-right' : ''}`}>
      {texto}
    </th>
  );

  return (
    <>
      <h3 className="mt-6 text-base font-semibold">
        {t('inventario.transferencias.ficha.lineas')}
      </h3>

      <p className="mt-1 text-sm text-neutral-700">
        {t('inventario.transferencias.ficha.nota')}
        {!conValor && ` ${t('inventario.transferencias.ficha.notaDelBorrador')}`}
      </p>

      {transferencia.lineas.length > 0 && (
        <BotonDeExportar exportar={() => exportarLineas(transferencia.id)} />
      )}

      {/* A 320 px, la tabla corre ella, y no la página (WCAG 1.4.10, ítem 2.20). `relative`
          recorta también lo que va en absoluto, como un `sr-only`, que si no la empuja. */}
      <div className="relative mt-2 overflow-x-auto">
        <table className="min-w-full border-collapse text-sm">
          <caption className="sr-only">{t('inventario.transferencias.ficha.tabla')}</caption>
          <thead>
            <tr className="border-b border-neutral-300 text-left">
              {columna(t('inventario.transferencias.ficha.numero'))}
              {columna(t('inventario.transferencias.ubicacionOrigen'))}
              {columna(t('inventario.transferencias.ubicacionDestino'))}
              {columna(t('inventario.transferencias.ficha.articulo'))}
              {columna(t('inventario.transferencias.ficha.cantidad'), true)}
              {columna(t('inventario.transferencias.ficha.factor'), true)}
              {columna(t('inventario.transferencias.ficha.enUnidadBase'), true)}
              {columna(t('inventario.transferencias.ficha.loteOSerie'))}
              {conValor && columna(t('inventario.transferencias.ficha.valor'), true)}
            </tr>
          </thead>
          <tbody>
            {transferencia.lineas.map((linea) => (
              <Fila
                key={linea.numero}
                linea={linea}
                divisa={transferencia.divisa}
                conValor={conValor}
              />
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
  linea: LineaDeTransferencia;
  divisa: string;
  conValor: boolean;
}): React.JSX.Element {
  const { i18n } = useTranslation();
  const sale = useNombre('ubicacion', linea.ubicacionOrigenId);
  const entra = useNombre('ubicacion', linea.ubicacionDestinoId);
  const articulo = useNombre('articulo', linea.articuloId);
  const unidad = useNombre('unidad', linea.unidadIntroducidaId);
  const idioma = i18n.language;

  return (
    <tr className="border-b border-neutral-200">
      <th scope="row" className="py-2 pr-4 text-left font-normal">
        {linea.numero}
      </th>
      <td className="py-2 pr-4">{sale.corto}</td>
      <td className="py-2 pr-4">{entra.corto}</td>
      <td className="py-2 pr-4">{articulo.largo}</td>
      <td className="py-2 pr-4 text-right whitespace-nowrap">
        {`${cantidadLegible(linea.cantidadIntroducida, idioma)} ${unidad.corto}`}
      </td>
      <td className="py-2 pr-4 text-right">{cantidadLegible(linea.factorAUnidadBase, idioma)}</td>
      <td className="py-2 pr-4 text-right">
        {cantidadLegible(linea.cantidadEnUnidadBase, idioma)}
      </td>
      <td className="py-2 pr-4">{linea.codigoDeLote ?? linea.numeroDeSerie ?? ''}</td>
      {conValor && (
        <td className="py-2 pr-4 text-right whitespace-nowrap">
          {linea.valor === null ? '' : importeLegible(linea.valor, divisa, idioma)}
        </td>
      )}
    </tr>
  );
}
