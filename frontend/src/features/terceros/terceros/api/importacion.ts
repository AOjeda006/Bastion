import type { Eleccion, InformeDeImportacion, MotivoDeRechazo } from '../model/importacion.ts';
import { api } from '@/shared/api/cliente.ts';
import type { components } from '@/shared/api/esquema.ts';
import { enteroDelContrato } from '@/shared/api/enteros.ts';
import { fallo } from '@/shared/api/errores.ts';

type InformeDto = components['schemas']['InformeDeImportacionDto'];
type MotivoDto = components['schemas']['MotivoDeRechazo'];

/** La cabecera de la clave de idempotencia, tal como la lee el filtro del servidor. */
const CABECERA_DE_IDEMPOTENCIA = 'Idempotency-Key';

const MOTIVOS: Record<MotivoDto, MotivoDeRechazo> = {
  'numero-de-campos-distinto': 'numeroDeCamposDistinto',
  'comillas-mal-colocadas': 'comillasMalColocadas',
  obligatorio: 'obligatorio',
  'demasiado-largo': 'demasiadoLargo',
  'formato-no-valido': 'formatoNoValido',
  'no-valido': 'noValido',
  'ni-cliente-ni-proveedor': 'niClienteNiProveedor',
  'ya-existe': 'yaExiste',
  'repetida-en-el-fichero': 'repetidaEnElFichero',
};

function motivoDe(texto: string): MotivoDeRechazo {
  return Object.hasOwn(MOTIVOS, texto) ? MOTIVOS[texto as MotivoDto] : 'desconocido';
}

function traducir(dto: InformeDto): InformeDeImportacion {
  return {
    leidas: enteroDelContrato(dto.leidas),
    importadas: enteroDelContrato(dto.importadas),
    rechazadas: enteroDelContrato(dto.rechazadas),
    rechazos: dto.rechazos.map((rechazo) => ({
      columna: rechazo.columna,
      motivo: motivoDe(rechazo.motivo),
      lineas: rechazo.lineas.map(enteroDelContrato),
    })),
  };
}

/**
 * Manda el fichero a importar, con su clave, y devuelve el informe.
 *
 * **Los bytes, tal cual, y nunca el texto.** El servidor decide la codificación mirando los bytes —UTF-8,
 * o Windows-1252 si no lo son—, y leer el fichero como texto en el navegador lo decodificaría como UTF-8:
 * cada «ñ» de un CSV de Excel en Windows-1252 llegaría convertida en el carácter de reemplazo, y la
 * razón social se importaría rota sin que nadie lo notara.
 *
 * Por eso el cuerpo lo pone `bodySerializer` y no `body`. El contrato tipa un cuerpo binario como
 * `string`, que es lo único que `openapi-typescript` sabe decir de `format: binary`, y lo que se manda
 * no es una cadena: la cadena vacía que va en `body` no sale nunca de aquí. Lo que sí se comprueba contra
 * el contrato es la ruta y la forma del informe.
 */
export async function importarTerceros(eleccion: Eleccion): Promise<InformeDeImportacion> {
  const bytes = await eleccion.fichero.arrayBuffer();

  const { data, error, response } = await api.POST('/api/v1/terceros/terceros/importacion', {
    body: '',
    bodySerializer: () => bytes,
    headers: {
      'Content-Type': 'text/csv',
      [CABECERA_DE_IDEMPOTENCIA]: eleccion.clave,
    },
  });

  if (data === undefined) {
    throw fallo(response.status, error);
  }

  return traducir(data);
}
