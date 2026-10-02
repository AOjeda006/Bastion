import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';

import { clavesDeArticulos } from '../api/claves.ts';
import {
  agregarCodigoDeBarras,
  consultarCodigosDeBarras,
  cuerpoDelAlta,
  quitarCodigoDeBarras,
} from '../api/codigosDeBarras.ts';
import { consultarFicha } from '../api/consultas.ts';
import { NIVELES_DE_GTIN, type CodigoDeBarras, type NivelDeGtin } from '../model/codigoDeBarras.ts';
import {
  RECHAZO_DE_LAS_UNIDADES,
  RECHAZO_DEL_NIVEL,
  RECHAZOS_DEL_GTIN,
  esRechazoDeCampo,
  esquemaDeAltaDeGtin,
  type DatosDeAltaDeGtin,
} from '../model/esquemaDeAltaDeGtin.ts';
import { intentoPara, type IntentoDeAlta } from '../model/intentoDeAlta.ts';
import { tipoDeFallo } from '@/shared/api/errores.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';
import { Cargando, Fallo, Vacio } from '@/shared/ui/Estados.tsx';
import { useTextoDeFallo } from '@/shared/ui/useTextoDeFallo.ts';

/** Lo que la pantalla acaba de hacer, o por qué no ha podido quitar. Uno a la vez: el último. */
type Aviso =
  | { readonly clase: 'agregado' | 'quitado'; readonly gtin: string }
  | { readonly clase: 'fallo'; readonly error: unknown };

const DEL_GTIN: ReadonlySet<string> = new Set(RECHAZOS_DEL_GTIN);

/**
 * Los códigos de barras de un artículo: los que tiene, el alta y la baja (ítem 2.10, ADR-0051).
 *
 * <b>Leer es del permiso de ver el artículo; dar de alta y quitar, de los suyos.</b> Quien solo ve
 * no encuentra ni el formulario ni los botones. La interfaz esconde, el servidor autoriza.
 *
 * <b>El formulario replica lo que puede y deja el resto al servidor.</b> El largo y el dígito de
 * control se dicen sin ir a la red; la tabla de prefijos y el duplicado, solo allí. Lo diga quien lo
 * diga, el rechazo va en su campo y con el mismo texto, porque los mensajes del esquema son los
 * `type` de la API (ADR-0030).
 *
 * <b>Quitar se confirma en la fila.</b> La baja borra de verdad, y el proyecto no tiene una ventana
 * modal propia que reutilizar: la pregunta sale en el sitio de los botones, con el foco en
 * «Cancelar», y Escape la cierra.
 *
 * <b>Lo que ya se enseñaba no se retira al volver a leerlo.</b> Tras un alta o una baja la lista se
 * lee otra vez, y si esa lectura falla, la tabla y el aviso se quedan, con el fallo encima. Sin eso,
 * un alta que entró desaparecería de la vista junto con la confirmación de que entró.
 */
export function PaginaDeCodigosDeBarras(): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const sesion = useSesionAbierta();
  const cache = useQueryClient();
  const { id = '' } = useParams();
  const [aviso, setAviso] = useState<Aviso | null>(null);
  // Cada aviso monta su párrafo de nuevo: el lector de pantalla lo anuncia aunque repita el texto
  // del anterior.
  const [numeroDeAviso, setNumeroDeAviso] = useState(0);
  // Cada alta que sale bien monta el formulario de nuevo: vacío, con su intento por estrenar y sin
  // el «ya se envió» que deja `handleSubmit`, que haría validar desde la primera cifra del siguiente.
  const [altas, setAltas] = useState(0);

  const puedeAgregar = concede(sesion, PERMISOS.codigoBarrasAgregar);
  const puedeQuitar = concede(sesion, PERMISOS.codigoBarrasQuitar);

  const ficha = useQuery({
    queryKey: clavesDeArticulos.una(id),
    queryFn: () => consultarFicha(id),
  });

  const codigos = useQuery({
    queryKey: clavesDeArticulos.codigosDeBarras(id),
    queryFn: () => consultarCodigosDeBarras(id),
  });

  const avisar = (siguiente: Aviso | null): void => {
    setAviso(siguiente);
    setNumeroDeAviso((numero) => numero + 1);
  };

  const releer = (): Promise<void> =>
    cache.invalidateQueries({ queryKey: clavesDeArticulos.codigosDeBarras(id) });

  const volver = (
    <p className="mt-6 text-sm">
      <Link to="/articulos" className="underline">
        {t('catalogo.articulos.gestionDeCodigosDeBarras.volver')}
      </Link>
    </p>
  );

  if (ficha.isPending || codigos.isPending) {
    return (
      <>
        <Cargando que={t('catalogo.articulos.gestionDeCodigosDeBarras.cargando')} />
        {volver}
      </>
    );
  }

  const fallo = (error: unknown, reintentar: () => unknown): React.JSX.Element => (
    <Fallo
      mensaje={textoDeFallo(error)}
      alReintentar={() => {
        void reintentar();
      }}
    />
  );

  // Sin datos, el fallo es la pantalla entera. La ficha primero: si el artículo no se puede leer,
  // su lista de códigos no tiene a quién pertenecer, y el motivo que importa es el suyo.
  if (ficha.data === undefined) {
    return (
      <>
        {fallo(ficha.error, ficha.refetch)}
        {volver}
      </>
    );
  }

  if (codigos.data === undefined) {
    return (
      <>
        {fallo(codigos.error, codigos.refetch)}
        {volver}
      </>
    );
  }

  const { articulo } = ficha.data;

  return (
    <>
      <dl className="mt-4 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-sm">
        <dt className="font-medium">{t('catalogo.articulos.gestionDeCodigosDeBarras.articulo')}</dt>
        <dd>
          <span className="font-mono">{articulo.codigo}</span> · {articulo.descripcion}
        </dd>
      </dl>

      {/* Con datos, el fallo de volver a leer va encima y no en su lugar. */}
      {codigos.isError && fallo(codigos.error, codigos.refetch)}

      <Avisos aviso={aviso} numero={numeroDeAviso} textoDeFallo={textoDeFallo} />

      {codigos.data.length === 0 ? (
        <Vacio mensaje={t('catalogo.articulos.gestionDeCodigosDeBarras.ninguno')} />
      ) : (
        <table className="mt-4 w-full border-collapse text-sm">
          <caption className="sr-only">
            {t('catalogo.articulos.gestionDeCodigosDeBarras.tabla', { codigo: articulo.codigo })}
          </caption>
          <thead>
            <tr className="border-b border-neutral-300 text-left">
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('catalogo.articulos.gestionDeCodigosDeBarras.gtin')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('catalogo.articulos.gestionDeCodigosDeBarras.nivel')}
              </th>
              <th scope="col" className="py-2 pr-4 font-medium">
                {t('catalogo.articulos.gestionDeCodigosDeBarras.unidades')}
              </th>
              {puedeQuitar && (
                <th scope="col" className="py-2 pr-4 font-medium">
                  {t('catalogo.articulos.gestionDeCodigosDeBarras.acciones')}
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {codigos.data.map((codigo) => (
              <Fila
                key={codigo.id}
                codigo={codigo}
                puedeQuitar={puedeQuitar}
                // Se espera a la lista nueva: hasta que llega, la fila sigue en «Quitando…» y no
                // ofrece quitarla otra vez.
                alQuitar={async () => {
                  avisar({ clase: 'quitado', gtin: codigo.gtin });
                  await releer();
                }}
                alFallar={(error) => {
                  avisar({ clase: 'fallo', error });
                  // Lo más probable es que alguien la quitara antes: la lista se lee otra vez para
                  // que deje de ofrecerla.
                  void releer();
                }}
              />
            ))}
          </tbody>
        </table>
      )}

      {puedeAgregar && (
        <FormularioDeAlta
          key={altas}
          articuloId={articulo.id}
          // Lo normal es dar de alta varios seguidos: tras cada uno, el foco vuelve al número.
          enfocarAlMontar={altas > 0}
          alEmpezar={() => {
            setAviso(null);
          }}
          alAgregar={async (codigo) => {
            avisar({ clase: 'agregado', gtin: codigo.gtin });
            setAltas((numero) => numero + 1);
            await releer();
          }}
        />
      )}

      {volver}
    </>
  );
}

/**
 * El aviso de lo último que ha pasado.
 *
 * <b>La región de estado está siempre montada</b>, vacía si no hay nada que decir, y lo que cambia
 * es su contenido: un lector de pantalla no anuncia de forma fiable una región que nace ya con el
 * texto dentro. El fallo sí nace con él, porque un `alert` se anuncia al aparecer.
 */
function Avisos({
  aviso,
  numero,
  textoDeFallo,
}: {
  aviso: Aviso | null;
  numero: number;
  textoDeFallo: (error: unknown) => string;
}): React.JSX.Element {
  const { t } = useTranslation();
  const parrafo = useRef<HTMLParagraphElement>(null);

  // Tras quitar, o tras no poder, la fila con el botón que tenía el foco desaparece o va a
  // desaparecer; el foco viene aquí en vez de caerse al principio de la página. Una vez por aviso.
  // Tras un alta no: el foco se queda en el formulario.
  useEffect(() => {
    if (aviso !== null && aviso.clase !== 'agregado') {
      parrafo.current?.focus();
    }
  }, [aviso, numero]);

  return (
    <>
      {/* Sin clases que la escondan cuando está vacía: lo que no se pinta sale también del árbol
          de accesibilidad, y una región que entra en él con el texto ya puesto no se anuncia. */}
      <div role="status">
        {aviso !== null && aviso.clase !== 'fallo' && (
          <p
            key={numero}
            tabIndex={-1}
            ref={parrafo}
            className="mt-4 rounded border border-green-300 bg-green-50 p-3 text-sm text-green-900"
          >
            {aviso.clase === 'agregado'
              ? t('catalogo.articulos.gestionDeCodigosDeBarras.agregado', { gtin: aviso.gtin })
              : t('catalogo.articulos.gestionDeCodigosDeBarras.quitado', { gtin: aviso.gtin })}
          </p>
        )}
      </div>

      {aviso?.clase === 'fallo' && (
        <p
          key={numero}
          role="alert"
          tabIndex={-1}
          ref={parrafo}
          className="mt-4 rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900"
        >
          {textoDeFallo(aviso.error)}
        </p>
      )}
    </>
  );
}

function Fila({
  codigo,
  puedeQuitar,
  alQuitar,
  alFallar,
}: {
  codigo: CodigoDeBarras;
  puedeQuitar: boolean;
  alQuitar: () => Promise<void>;
  alFallar: (error: unknown) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const [confirmando, setConfirmando] = useState(false);
  const botonQuitar = useRef<HTMLButtonElement>(null);
  const botonCancelar = useRef<HTMLButtonElement>(null);
  // Al cerrar la pregunta sin quitar, el foco vuelve al «Quitar» de esta fila, que es de donde
  // salió. Al abrirla, va a «Cancelar»: lo que no se puede deshacer no se hace con un Intro.
  const devolverElFoco = useRef(false);

  useEffect(() => {
    if (confirmando) {
      botonCancelar.current?.focus();
    } else if (devolverElFoco.current) {
      devolverElFoco.current = false;
      botonQuitar.current?.focus();
    }
  }, [confirmando]);

  const cerrar = (): void => {
    devolverElFoco.current = true;
    setConfirmando(false);
  };

  const baja = useMutation({
    mutationFn: () => quitarCodigoDeBarras(codigo.id),
    onSuccess: alQuitar,
    onError: (error) => {
      // Sin devolver el foco a la fila: lo más probable es que vaya a desaparecer, y el foco va al
      // aviso del fallo.
      setConfirmando(false);
      alFallar(error);
    },
  });

  // Mientras se quita, y después, hasta que la fila desaparece, la pregunta no se vuelve a ofrecer.
  const ocupada = baja.isPending || baja.isSuccess;

  // En los dos botones y no en el grupo, que no es un elemento con el que se interactúe: el foco
  // está siempre en uno de ellos mientras la pregunta está abierta.
  const alPulsar = (evento: React.KeyboardEvent): void => {
    if (evento.key === 'Escape' && !ocupada) {
      cerrar();
    }
  };

  return (
    <tr className="border-b border-neutral-200 align-top">
      <td className="py-2 pr-4 font-mono">{codigo.gtin}</td>
      <td className="py-2 pr-4">
        <Nivel nivel={codigo.nivel} />
      </td>
      <td className="py-2 pr-4">{codigo.unidades}</td>
      {puedeQuitar && (
        <td className="py-2 pr-4">
          {confirmando ? (
            <div
              role="group"
              aria-labelledby={`confirmar-${codigo.id}`}
              className="flex flex-wrap items-center gap-2"
            >
              <span id={`confirmar-${codigo.id}`}>
                {t('catalogo.articulos.gestionDeCodigosDeBarras.confirmar', { gtin: codigo.gtin })}
              </span>
              <button
                type="button"
                disabled={ocupada}
                onClick={() => {
                  baja.mutate();
                }}
                onKeyDown={alPulsar}
                className="rounded bg-red-800 px-2 py-1 text-xs text-white disabled:opacity-50"
              >
                {ocupada
                  ? t('catalogo.articulos.gestionDeCodigosDeBarras.quitando')
                  : t('catalogo.articulos.gestionDeCodigosDeBarras.siQuitar')}
              </button>
              <button
                type="button"
                disabled={ocupada}
                ref={botonCancelar}
                onClick={cerrar}
                onKeyDown={alPulsar}
                className="rounded border border-neutral-300 px-2 py-1 text-xs disabled:opacity-50"
              >
                {t('catalogo.articulos.gestionDeCodigosDeBarras.cancelar')}
              </button>
            </div>
          ) : (
            <button
              type="button"
              // El texto visible es «Quitar», uno por fila; el nombre accesible dice cuál.
              aria-label={t('catalogo.articulos.gestionDeCodigosDeBarras.quitarElDe', {
                gtin: codigo.gtin,
              })}
              ref={botonQuitar}
              onClick={() => {
                setConfirmando(true);
              }}
              className="rounded border border-neutral-300 px-2 py-1 text-xs"
            >
              {t('catalogo.articulos.gestionDeCodigosDeBarras.quitar')}
            </button>
          )}
        </td>
      )}
    </tr>
  );
}

/** El nombre de un nivel, y qué se dice cuando llega uno que esta versión no conoce. */
function Nivel({ nivel }: { nivel: NivelDeGtin }): React.JSX.Element {
  const { t } = useTranslation();

  if (nivel === 'desconocido') {
    return (
      <span
        title={t('catalogo.articulos.niveles.desconocidoDetalle')}
        className="rounded border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-xs text-amber-900"
      >
        {t('catalogo.articulos.niveles.desconocido')}
      </span>
    );
  }

  return <>{t(`catalogo.articulos.niveles.${nivel}`)}</>;
}

function FormularioDeAlta({
  articuloId,
  enfocarAlMontar,
  alEmpezar,
  alAgregar,
}: {
  articuloId: string;
  enfocarAlMontar: boolean;
  alEmpezar: () => void;
  alAgregar: (codigo: CodigoDeBarras) => Promise<void>;
}): React.JSX.Element {
  const { t } = useTranslation();
  const textoDeFallo = useTextoDeFallo();
  const [rechazo, setRechazo] = useState<unknown>(null);
  // En una referencia y no en el estado: no se pinta, y el doble clic tiene que verla ya. Tras un
  // alta que sale bien no hace falta olvidarla: el formulario se monta de nuevo, con una vacía.
  const intento = useRef<IntentoDeAlta | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    control,
    formState: { errors, isSubmitting },
  } = useForm<DatosDeAltaDeGtin>({
    resolver: zodResolver(esquemaDeAltaDeGtin),
    defaultValues: { gtin: '', nivel: 'base', unidades: '' },
  });

  const esAgrupacion = useWatch({ control, name: 'nivel' }) !== 'base';

  useEffect(() => {
    if (enfocarAlMontar) {
      setFocus('gtin');
    }
  }, [enfocarAlMontar, setFocus]);

  const agregar = async (datos: DatosDeAltaDeGtin): Promise<void> => {
    setRechazo(null);
    alEmpezar();

    const cuerpo = cuerpoDelAlta(datos);
    intento.current = intentoPara(cuerpo, intento.current);

    let codigo: CodigoDeBarras;

    try {
      codigo = await agregarCodigoDeBarras(articuloId, cuerpo, intento.current.clave);
    } catch (error) {
      const tipo = tipoDeFallo(error);

      // El rechazo que es de un campo va en ese campo, con el foco; el resto, arriba.
      if (tipo !== null && DEL_GTIN.has(tipo)) {
        setError('gtin', { type: 'servidor', message: tipo }, { shouldFocus: true });
      } else if (tipo === RECHAZO_DE_LAS_UNIDADES && esAgrupacion) {
        setError('unidades', { type: 'servidor', message: tipo }, { shouldFocus: true });
      } else if (tipo === RECHAZO_DEL_NIVEL) {
        // `shouldFocus` no llega a un grupo de opciones: React Hook Form no guarda ahí un elemento
        // al que mandarlo. `setFocus` sí, a la primera.
        setError('nivel', { type: 'servidor', message: tipo });
        setFocus('nivel');
      } else {
        setRechazo(error);
      }

      return;
    }

    await alAgregar(codigo);
  };

  const texto = (mensaje: string | undefined): string | null => {
    if (mensaje === undefined) {
      return null;
    }

    return esRechazoDeCampo(mensaje) ? t(`errores.tipos.${mensaje}`) : mensaje;
  };

  const errorDelGtin = texto(errors.gtin?.message);
  const errorDelNivel = texto(errors.nivel?.message);
  const errorDeLasUnidades = texto(errors.unidades?.message);

  return (
    <form
      noValidate
      onSubmit={(evento) => {
        void handleSubmit(agregar)(evento);
      }}
      aria-labelledby="alta-de-gtin"
      className="mt-8 max-w-md space-y-4"
    >
      <h2 id="alta-de-gtin" className="text-lg font-semibold">
        {t('catalogo.articulos.gestionDeCodigosDeBarras.alta')}
      </h2>

      {rechazo !== null && (
        <p
          role="alert"
          className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900"
        >
          {textoDeFallo(rechazo)}
        </p>
      )}

      <div className="flex flex-col gap-1 text-sm">
        <label htmlFor="gtin" className="font-medium">
          {t('catalogo.articulos.gestionDeCodigosDeBarras.campoGtin')}
        </label>
        <input
          id="gtin"
          type="text"
          inputMode="numeric"
          autoComplete="off"
          aria-invalid={errorDelGtin !== null}
          aria-describedby={errorDelGtin === null ? 'gtin-pista' : 'gtin-pista gtin-error'}
          {...register('gtin')}
          className="rounded border border-neutral-300 px-2 py-1.5 font-mono"
        />
        <p id="gtin-pista" className="text-neutral-600">
          {t('catalogo.articulos.gestionDeCodigosDeBarras.pistaGtin')}
        </p>
        {errorDelGtin !== null && (
          <p id="gtin-error" role="alert" className="text-red-800">
            {errorDelGtin}
          </p>
        )}
      </div>

      {/* Como en la trazabilidad: el error del grupo va en su descripción, no en un
          `aria-invalid`, que ARIA 1.2 no admite ni en un grupo ni en un botón de opción. */}
      <fieldset
        aria-describedby={errorDelNivel === null ? 'nivel-pista' : 'nivel-pista nivel-error'}
      >
        <legend className="text-sm font-medium">
          {t('catalogo.articulos.gestionDeCodigosDeBarras.leyendaNivel')}
        </legend>
        <p id="nivel-pista" className="mt-1 text-sm text-neutral-600">
          {t('catalogo.articulos.gestionDeCodigosDeBarras.pistaNivel')}
        </p>
        <div className="mt-2 space-y-1">
          {NIVELES_DE_GTIN.map((valor) => (
            <label key={valor} className="flex items-center gap-2 text-sm">
              <input type="radio" value={valor} {...register('nivel')} />
              {t(`catalogo.articulos.niveles.${valor}`)}
            </label>
          ))}
        </div>
        {errorDelNivel !== null && (
          <p id="nivel-error" role="alert" className="mt-1 text-sm text-red-800">
            {errorDelNivel}
          </p>
        )}
      </fieldset>

      {/* Solo una caja o un palé dicen cuántas lleva: la base es una, y la pone el servidor. */}
      {esAgrupacion && (
        <div className="flex flex-col gap-1 text-sm">
          <label htmlFor="unidades" className="font-medium">
            {t('catalogo.articulos.gestionDeCodigosDeBarras.campoUnidades')}
          </label>
          <input
            id="unidades"
            type="text"
            inputMode="numeric"
            autoComplete="off"
            aria-invalid={errorDeLasUnidades !== null}
            aria-describedby={
              errorDeLasUnidades === null ? 'unidades-pista' : 'unidades-pista unidades-error'
            }
            {...register('unidades')}
            className="w-32 rounded border border-neutral-300 px-2 py-1.5"
          />
          <p id="unidades-pista" className="text-neutral-600">
            {t('catalogo.articulos.gestionDeCodigosDeBarras.pistaUnidades')}
          </p>
          {errorDeLasUnidades !== null && (
            <p id="unidades-error" role="alert" className="text-red-800">
              {errorDeLasUnidades}
            </p>
          )}
        </div>
      )}

      <button
        type="submit"
        disabled={isSubmitting}
        className="rounded bg-neutral-900 px-4 py-2 text-sm text-white disabled:opacity-50"
      >
        {isSubmitting
          ? t('catalogo.articulos.gestionDeCodigosDeBarras.agregando')
          : t('catalogo.articulos.gestionDeCodigosDeBarras.agregar')}
      </button>
    </form>
  );
}
