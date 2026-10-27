// Mide lo que cuesta cada ruta diferida del frontal, y lo que el usuario baja comprimido.
//
// Lo llama `presupuesto-del-frontal.sh`, después de medir el arranque, con el tope por ruta y la
// lista de lo que él ha contado como arranque. No se lanza solo.
//
// LO QUE MIDE
// -----------
//   ruta       = el fragmento de su pantalla
//              + lo que ese fragmento importa estáticamente, hasta el fondo, con sus hojas de
//                estilo
//              - lo que ya se pagó en el arranque
//              + los textos de la funcionalidad dueña, en el MAYOR de sus idiomas (ADR-0036 §3)
//
// Es lo que se descarga la PRIMERA vez que alguien entra en esa ruta, con el arranque ya en casa.
// Un fragmento compartido entre dos rutas se cuenta entero en las dos: quien solo abre una lo paga
// entero, y el tope es una promesa sobre el peor caso de cada una. Tope: 192 KiB por ruta.
//
// POR QUÉ CON EL MANIFIESTO, SI EL ADR-0036 LO DESCARTÓ
// ----------------------------------------------------
// Lo descartó para el ARRANQUE, y por una razón que sigue en pie: el manifiesto no dice cuáles de
// las importaciones dinámicas del fragmento de entrada se esperan antes de pintar. Para una RUTA la
// pregunta es otra —qué arrastra su fragmento—, y eso el `dist` no lo dice y el manifiesto sí: en
// el `dist` un fragmento compartido no lleva escrito quién lo importa. Así que el arranque se sigue
// leyendo del `index.html` más lo declarado, y las rutas, del manifiesto. Y como las dos fuentes
// pueden dar la misma lista para la parte que comparten —lo que el `index.html` referencia—, se
// comparan (ADR-0071).
//
// LO QUE AFIRMA ANTES DE MEDIR
// ----------------------------
// Que hay manifiesto y un solo punto de entrada; que el arranque que lee del manifiesto es el que
// ha contado el guion, salvo lo declarado, que tiene que ser un diccionario; que TODA importación
// dinámica de la entrada está clasificada —diccionario, textos de los `type`, textos de una
// funcionalidad o pantalla—; que las pantallas son exactamente las que importa `rutas.tsx`, en los
// dos sentidos; que cada funcionalidad tiene un fragmento de textos por idioma, y ninguno en el
// arranque; y que ningún otro fragmento importa nada en diferido, porque eso sería una descarga que
// no cuenta ninguna ruta. Sin esto, una medida que no encuentra nada da cero, y cero pasa el tope.
//
// LO COMPRIMIDO, INFORMATIVO
// --------------------------
// nginx sirve con `gzip on`, nivel 1 por omisión, para `text/html` y los tipos de `gzip_types`, y
// solo a partir de `gzip_min_length` (`deploy/nginx.conf`). Se mide igual: es lo que baja el
// usuario. No tiene tope, porque el tope vigila lo que el navegador tiene que leer y ejecutar, y
// eso es lo descomprimido.
//
// Uso: node presupuesto-por-ruta.mjs <dist> <tope-ruta-KiB> <fichero-del-arranque>...

import { appendFileSync, existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, posix } from 'node:path';
import { gzipSync } from 'node:zlib';

const [dist, topeTexto, ...delArranque] = process.argv.slice(2);
const TOPE_RUTA_KIB = Number(topeTexto);

const NIVEL_DE_NGINX = 1;
const MINIMO_DE_NGINX = 1024;
const COMPRIMIBLES = /\.(?:html|css|js|json|svg)$/;

function fallar(mensaje) {
  console.log(`::error::${mensaje}`);
  console.error(mensaje);
  process.exit(2);
}

function anotar(linea) {
  console.log(linea);
  console.log(`::notice::${linea}`);
  if (process.env.GITHUB_STEP_SUMMARY) {
    appendFileSync(process.env.GITHUB_STEP_SUMMARY, `${linea}\n`);
  }
}

// Hacia arriba, como el guion: un byte de más ya es un KiB de más.
const aKib = (bytes) => Math.ceil(bytes / 1024);

function bytesDe(fichero) {
  return statSync(join(dist, fichero)).size;
}

function comprimidoDe(fichero) {
  const bytes = bytesDe(fichero);

  return bytes >= MINIMO_DE_NGINX && COMPRIMIBLES.test(fichero)
    ? gzipSync(readFileSync(join(dist, fichero)), { level: NIVEL_DE_NGINX }).length
    : bytes;
}

const suma = (ficheros, medida) => [...ficheros].reduce((total, f) => total + medida(f), 0);

if (!dist || !Number.isInteger(TOPE_RUTA_KIB) || TOPE_RUTA_KIB <= 0) {
  fallar('uso: presupuesto-por-ruta.mjs <dist> <tope-ruta-KiB> <fichero-del-arranque>...');
}

// --- El manifiesto, y su único punto de entrada ------------------------------------------------

const RUTA_DEL_MANIFIESTO = join(dist, '.vite', 'manifest.json');
if (!existsSync(RUTA_DEL_MANIFIESTO)) {
  fallar(
    `no existe '${RUTA_DEL_MANIFIESTO}': sin él no se sabe qué arrastra cada ruta. Lo escribe ` +
      '`build.manifest` en `vite.config.ts`.',
  );
}

const manifiesto = JSON.parse(readFileSync(RUTA_DEL_MANIFIESTO, 'utf8'));
const entradas = Object.entries(manifiesto).filter(([, trozo]) => trozo.isEntry);

if (entradas.length !== 1 || entradas[0][0] !== 'index.html') {
  fallar(
    `el manifiesto tiene ${entradas.length} puntos de entrada ` +
      `(${entradas.map(([clave]) => clave).join(', ')}), y tiene que tener uno, 'index.html': ` +
      'con otro, el arranque no es el que mide el guion.',
  );
}

const [CLAVE_DE_ENTRADA, ENTRADA] = entradas[0];

/** Los ficheros de un trozo del manifiesto: el suyo, sus hojas de estilo y sus recursos. */
function ficherosDe(trozo) {
  return [trozo.file, ...(trozo.css ?? []), ...(trozo.assets ?? [])];
}

/** Las claves que un trozo importa estáticamente, hasta el fondo, sin pasar por las de `parar`. */
function cierreDe(clave, parar) {
  const vistas = new Set();
  const pendientes = [clave];

  while (pendientes.length > 0) {
    const actual = pendientes.pop();
    if (vistas.has(actual) || parar.has(actual)) continue;

    const trozo = manifiesto[actual];
    if (trozo === undefined) {
      fallar(`el manifiesto nombra '${actual}' como importación y no tiene su entrada.`);
    }

    vistas.add(actual);
    pendientes.push(...(trozo.imports ?? []));
  }

  return vistas;
}

// --- El arranque del manifiesto, contra el del guion --------------------------------------------

const clavesDelArranque = cierreDe(CLAVE_DE_ENTRADA, new Set());
const arranque = new Set([
  'index.html',
  ...[...clavesDelArranque].flatMap((c) => ficherosDe(manifiesto[c])),
]);
const contados = new Set(delArranque);

if (contados.size === 0) {
  fallar('el guion no ha pasado ningún fichero del arranque: no hay contra qué comparar.');
}

const sinContar = [...arranque].filter((f) => !contados.has(f));
if (sinContar.length > 0) {
  fallar(
    `el manifiesto pone en el arranque ${sinContar.join(', ')}, y el guion no lo ha contado: el ` +
      '`index.html` y el manifiesto han dejado de decir lo mismo.',
  );
}

// --- Las importaciones dinámicas de la entrada, clasificadas TODAS ------------------------------

const leer = (camino) => readFileSync(join(dirname(dist), camino), 'utf8');

function listaDe(fuente, nombre, patron, camino) {
  const linea = fuente.match(patron)?.[1];
  if (linea === undefined) {
    fallar(`no se ha encontrado '${nombre}' en '${camino}': el patrón ha dejado de casar.`);
  }

  const lista = [...linea.matchAll(/'([a-zA-Z-]+)'/g)].map(([, valor]) => valor);
  if (lista.length === 0) {
    fallar(`'${nombre}' está en '${camino}' pero no se le ha sacado ni un elemento.`);
  }

  return lista;
}

const IDIOMAS = listaDe(
  leer('src/app/i18n/idioma.ts'),
  'export const IDIOMAS',
  /export const IDIOMAS\s*=\s*\[([^\]]*)\]/,
  'src/app/i18n/idioma.ts',
);
const FUNCIONALIDADES = listaDe(
  leer('src/features/funcionalidades.ts'),
  'export const FUNCIONALIDADES',
  /export const FUNCIONALIDADES\s*=\s*\[([^\]]*)\]/,
  'src/features/funcionalidades.ts',
);

const diccionarios = new Map();
const tipos = new Map();
const textos = new Map();
const pantallas = new Map();
const sinClasificar = [];

for (const clave of ENTRADA.dynamicImports ?? []) {
  let encontrado;

  if ((encontrado = clave.match(/^src\/app\/i18n\/([a-z]{2})\.ts$/))) {
    diccionarios.set(encontrado[1], clave);
  } else if ((encontrado = clave.match(/^src\/app\/i18n\/tiposDeError\.([a-z]{2})\.ts$/))) {
    tipos.set(encontrado[1], clave);
  } else if ((encontrado = clave.match(/^src\/app\/i18n\/([a-zA-Z]+)\.([a-z]{2})\.ts$/))) {
    if (!FUNCIONALIDADES.includes(encontrado[1])) {
      sinClasificar.push(`${clave} (textos de '${encontrado[1]}', que no es una funcionalidad)`);
      continue;
    }
    textos.set(`${encontrado[1]}.${encontrado[2]}`, clave);
  } else if ((encontrado = clave.match(/^src\/features\/([a-z]+)\/.+\.tsx$/))) {
    if (!FUNCIONALIDADES.includes(encontrado[1])) {
      sinClasificar.push(`${clave} (pantalla de '${encontrado[1]}', que no es una funcionalidad)`);
      continue;
    }
    pantallas.set(clave, encontrado[1]);
  } else if (/^src\/app\/paginas\/.+\.tsx$/.test(clave)) {
    pantallas.set(clave, 'armazon');
  } else {
    sinClasificar.push(clave);
  }
}

if (sinClasificar.length > 0) {
  fallar(
    `la entrada importa en diferido ${sinClasificar.join(', ')}, y no es ni un diccionario, ni ` +
      'los textos de los type, ni los de una funcionalidad, ni una pantalla: nadie lo está ' +
      'midiendo.',
  );
}

const enDiferidoDesdeOtro = Object.entries(manifiesto)
  .filter(([clave, trozo]) => clave !== CLAVE_DE_ENTRADA && (trozo.dynamicImports ?? []).length > 0)
  .map(([clave]) => clave);

if (enDiferidoDesdeOtro.length > 0) {
  fallar(
    `${enDiferidoDesdeOtro.join(', ')} importa algo en diferido: es una descarga que no cuenta ` +
      'ninguna ruta. Hay que decidir de quién es antes de medirla.',
  );
}

// Lo que el guion contó y el manifiesto no pone en el arranque tiene que ser un diccionario: es la
// parte declarada del ADR-0036, y no puede ser otra cosa.
const ficherosDeDiccionario = new Set([...diccionarios.values()].map((c) => manifiesto[c].file));
const deMas = [...contados].filter((f) => !arranque.has(f) && !ficherosDeDiccionario.has(f));
if (deMas.length > 0) {
  fallar(
    `el guion cuenta en el arranque ${deMas.join(', ')}, que el manifiesto no pone allí y no ` +
      'es un diccionario del arranque.',
  );
}

// --- Las pantallas, contra las que importa la tabla de rutas ------------------------------------

const DE_LA_TABLA = new Set(
  [...leer('src/app/rutas.tsx').matchAll(/import\('@\/([^']+)'\)/g)].map(([, c]) => `src/${c}`),
);

const faltan = [...DE_LA_TABLA].filter((c) => !pantallas.has(c));
const sobran = [...pantallas.keys()].filter((c) => !DE_LA_TABLA.has(c));

if (DE_LA_TABLA.size === 0 || pantallas.size === 0) {
  fallar(
    `la tabla de rutas importa ${DE_LA_TABLA.size} pantallas y el manifiesto tiene ` +
      `${pantallas.size}: una de las dos lecturas no ha encontrado nada.`,
  );
}

if (faltan.length > 0 || sobran.length > 0) {
  fallar(
    `las pantallas del manifiesto no son las de 'src/app/rutas.tsx'. Faltan en el manifiesto: ` +
      `[${faltan.join(', ')}]. Sobran: [${sobran.join(', ')}].`,
  );
}

// --- Los textos de cada funcionalidad: uno por idioma, y fuera del arranque ----------------------

for (const funcionalidad of FUNCIONALIDADES) {
  for (const idioma of IDIOMAS) {
    const clave = textos.get(`${funcionalidad}.${idioma}`);
    if (clave === undefined) {
      fallar(
        `no hay fragmento de los textos de '${funcionalidad}' en '${idioma}': o han vuelto al ` +
          'diccionario del arranque, o algo los importa por su valor y se han quedado dentro de ' +
          'otro fragmento (ADR-0071).',
      );
    }
    if (clavesDelArranque.has(clave) || arranque.has(manifiesto[clave].file)) {
      fallar(`los textos de '${funcionalidad}' en '${idioma}' están en el arranque.`);
    }
  }
}

if (textos.size !== FUNCIONALIDADES.length * IDIOMAS.length) {
  fallar(
    `hay ${textos.size} fragmentos de textos y tendría que haber ` +
      `${FUNCIONALIDADES.length * IDIOMAS.length}: uno por funcionalidad y por idioma.`,
  );
}

// --- Cada ruta: su cierre, menos el arranque, más los textos de su dueño ------------------------

const ficherosDelCierre = (clave) =>
  new Set([...cierreDe(clave, clavesDelArranque)].flatMap((c) => ficherosDe(manifiesto[c])));

const rutas = [...pantallas].map(([clave, duenio]) => {
  const propios = ficherosDelCierre(clave);

  let textosDelDuenio = new Set();
  let detalleDeTextos = 'sin textos propios: los del armazón llegan con el arranque';

  if (duenio !== 'armazon') {
    const porIdioma = IDIOMAS.map((idioma) => {
      const ficheros = ficherosDelCierre(textos.get(`${duenio}.${idioma}`));
      const solo = [...ficheros].filter((f) => !propios.has(f));

      return { idioma, ficheros: new Set(solo), bytes: suma(solo, bytesDe) };
    }).sort((a, b) => b.bytes - a.bytes);

    textosDelDuenio = porIdioma[0].ficheros;
    detalleDeTextos =
      `con sus textos en ${porIdioma[0].idioma}, el mayor: ` +
      porIdioma.map(({ idioma, bytes }) => `${idioma} ${bytes} B`).join(', ');
  }

  const todos = new Set([...propios, ...textosDelDuenio]);

  return {
    nombre: posix.basename(clave).replace(/\.tsx$/, ''),
    duenio,
    ficheros: todos.size,
    bytes: suma(todos, bytesDe),
    comprimido: suma(todos, comprimidoDe),
    detalleDeTextos,
  };
});

rutas.sort((a, b) => b.bytes - a.bytes);

if (rutas.some((ruta) => ruta.bytes === 0)) {
  fallar('alguna ruta mide cero bytes: su fragmento no se ha resuelto dentro del dist.');
}

// --- El total, para lo comprimido: lo servido, sin mapas ni manifiesto --------------------------

function servidos(relativa = '') {
  return readdirSync(join(dist, relativa), { withFileTypes: true }).flatMap((entrada) => {
    const camino = relativa === '' ? entrada.name : `${relativa}/${entrada.name}`;
    if (entrada.isDirectory()) return camino === '.vite' ? [] : servidos(camino);

    return camino.endsWith('.map') ? [] : [camino];
  });
}

const total = servidos();

// --- El desglose, y solo después los topes ------------------------------------------------------

console.log(
  'Rutas diferidas — lo que se descarga al entrar por primera vez, con el arranque ya en casa:',
);
for (const ruta of rutas) {
  console.log(
    `  ${String(ruta.bytes).padStart(8)} B  ${String(ruta.comprimido).padStart(7)} B comprimido  ` +
      `${ruta.nombre} (${ruta.duenio}): ${ruta.ficheros} ` +
      `${ruta.ficheros === 1 ? 'fichero' : 'ficheros'}, ${ruta.detalleDeTextos}`,
  );
}

const [mayor] = rutas;
anotar(
  `Frontal · la ruta diferida más pesada, ${mayor.nombre}, ${aKib(mayor.bytes)}/${TOPE_RUTA_KIB} ` +
    `KiB · ${rutas.length} rutas, cada una con los textos de su módulo`,
);
anotar(
  `Frontal · comprimido, como lo sirve nginx (gzip ${NIVEL_DE_NGINX}), informativo: arranque ` +
    `${aKib(suma(contados, comprimidoDe))} KiB · la ruta más pesada ` +
    `${aKib(mayor.comprimido)} KiB · total servido ${aKib(suma(total, comprimidoDe))} KiB`,
);

const excedidas = rutas.filter((ruta) => aKib(ruta.bytes) > TOPE_RUTA_KIB);
for (const ruta of excedidas) {
  console.log(
    `::error::La ruta diferida ${ruta.nombre} supera su presupuesto (${aKib(ruta.bytes)} KiB > ` +
      `${TOPE_RUTA_KIB} KiB). Es lo que espera quien entra en ella por primera vez: mirar qué ` +
      'dependencia ha entrado en su pantalla, o qué fragmento compartido ha engordado.',
  );
}

process.exit(excedidas.length > 0 ? 1 : 0);
