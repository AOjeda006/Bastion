using System.Text;
using System.Text.Unicode;
using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Application.Importacion;

/// <summary>
/// Lee un fichero CSV del dialecto de una hoja de cálculo en español, y lo que no es de ese dialecto
/// lo rechaza con nombre en vez de adivinarlo (ADR-0034 §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Recibe bytes y no una corriente</b>: cuando llegan aquí, el borde ya los ha leído con su tope
/// (<c>LectorAcotadoDelCuerpo</c>). Este lector no puede imponer un tope de tamaño porque, para
/// cuando lo llaman, la memoria ya se ha gastado; el que sí impone es el de filas.
/// </para>
/// <para>
/// <b>El separador es el punto y coma, y no se detecta.</b> Si la cabecera no casa, se prueba si
/// casaría separada por comas o por tabuladores, solo para poder llamar al error por su nombre; el
/// fichero nunca se lee con otro separador. Un fichero de una sola columna no casa con ninguno, así
/// que es una cabecera que no vale.
/// </para>
/// <para>
/// <b>La codificación sí se detecta, y el detector falla, y así:</b> primero UTF-8, y si los bytes no
/// son UTF-8 válido, Windows-1252. Un Windows-1252 cuyos bytes forman UTF-8 válido por casualidad se
/// lee como UTF-8: hace falta una letra entre «Â» y «ô» seguida de uno o dos caracteres entre «€» y
/// «¿», como «Ã±», que en español no se escribe. Un texto español real en Windows-1252 —una «ñ» o una
/// «á» seguidas de una letra— nunca es UTF-8 válido, y uno solo ASCII es el mismo en las dos.
/// </para>
/// </remarks>
public static class LectorCsv
{
    /// <summary>El separador de campos del dialecto.</summary>
    public const char Separador = ';';

    private const int PaginaDeCodigosDeWindowsOccidental = 1252;

    private static readonly Encoding s_windows1252 =
        CodePagesEncodingProvider.Instance.GetEncoding(PaginaDeCodigosDeWindowsOccidental)
        ?? throw new InvalidOperationException("La plataforma no trae la página de códigos Windows-1252.");

    /// <summary>Lee el fichero entero y lo parte en filas.</summary>
    /// <param name="bytes">El contenido del fichero, tal como llegó.</param>
    /// <param name="cabecera">Las columnas que tiene que traer la primera fila, en su orden y tal cual.</param>
    /// <param name="topeDeFilas">Cuántas filas se admiten detrás de la cabecera, contando las vacías.</param>
    /// <returns>Las filas con datos, o el error de fichero que impide saber qué es una fila.</returns>
    public static Resultado<HojaCsv> Leer(ReadOnlySpan<byte> bytes, IReadOnlyList<string> cabecera, int topeDeFilas)
    {
        ArgumentNullException.ThrowIfNull(cabecera);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topeDeFilas);

        string? texto = Decodificar(bytes);

        if (texto is null)
        {
            return Resultado.Fallo<HojaCsv>(ErroresDeImportacion.CodificacionNoAdmitida());
        }

        var troceador = new Troceador(texto, Separador);
        List<string> campos = [];
        HashSet<int> malColocadas = [];

        Troceo primera = troceador.Siguiente(campos, malColocadas);

        if (primera != Troceo.Registro)
        {
            return Resultado.Fallo<HojaCsv>(ErrorDe(primera, cabecera));
        }

        if (malColocadas.Count > 0 || !campos.SequenceEqual(cabecera, StringComparer.Ordinal))
        {
            return Resultado.Fallo<HojaCsv>(
                CasariaCon(texto, ',', cabecera) || CasariaCon(texto, '\t', cabecera)
                    ? ErroresDeImportacion.SeparadorNoAdmitido()
                    : ErroresDeImportacion.CabeceraNoValida(cabecera));
        }

        List<FilaCsv> filas = [];
        int linea = 1;

        while (true)
        {
            Troceo troceo = troceador.Siguiente(campos, malColocadas);

            if (troceo == Troceo.FinDelTexto)
            {
                return Resultado.Correcto(new HojaCsv(filas));
            }

            if (troceo != Troceo.Registro)
            {
                return Resultado.Fallo<HojaCsv>(ErrorDe(troceo, cabecera));
            }

            linea++;

            // El tope cuenta las vacías y se comprueba al llegar a la primera que sobra, no al final:
            // un fichero de un millón de filas de un byte no se trocea entero para decirle que no.
            if (linea - 1 > topeDeFilas)
            {
                return Resultado.Fallo<HojaCsv>(ErroresDeImportacion.DemasiadasFilas(topeDeFilas));
            }

            if (campos.TrueForAll(campo => campo.Length == 0) && malColocadas.Count == 0)
            {
                continue;
            }

            filas.Add(new FilaCsv(linea, [.. campos], new HashSet<int>(malColocadas), campos.Count == cabecera.Count));
        }
    }

    // Null es «no es del dialecto». Lo que decide cada rama está en el comentario de la clase.
    private static string? Decodificar(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> bomDeUtf8 = [0xEF, 0xBB, 0xBF];
        ReadOnlySpan<byte> bomDeUtf16LittleEndian = [0xFF, 0xFE];
        ReadOnlySpan<byte> bomDeUtf16BigEndian = [0xFE, 0xFF];
        ReadOnlySpan<byte> bomDeUtf32BigEndian = [0x00, 0x00, 0xFE, 0xFF];

        // El BOM de UTF-32 little endian empieza por el de UTF-16 little endian, así que lo cubre la
        // misma comprobación; el de big endian empieza por nulos, y lo cubre también la del byte nulo.
        // Los dos se nombran igual porque ninguno lo escribe una hoja de cálculo en español.
        if (bytes.StartsWith(bomDeUtf16LittleEndian)
            || bytes.StartsWith(bomDeUtf16BigEndian)
            || bytes.StartsWith(bomDeUtf32BigEndian)
            || bytes.Contains((byte)0))
        {
            return null;
        }

        if (bytes.StartsWith(bomDeUtf8))
        {
            ReadOnlySpan<byte> resto = bytes[bomDeUtf8.Length..];

            // Con BOM, el fichero DICE que es UTF-8. Si luego no lo es, no se le busca otra lectura.
            return Utf8.IsValid(resto) ? Encoding.UTF8.GetString(resto) : null;
        }

        return Utf8.IsValid(bytes) ? Encoding.UTF8.GetString(bytes) : s_windows1252.GetString(bytes);
    }

    private static bool CasariaCon(string texto, char otroSeparador, IReadOnlyList<string> cabecera)
    {
        List<string> campos = [];
        HashSet<int> malColocadas = [];

        return new Troceador(texto, otroSeparador).Siguiente(campos, malColocadas) == Troceo.Registro
            && malColocadas.Count == 0
            && campos.SequenceEqual(cabecera, StringComparer.Ordinal);
    }

    private static ErrorDeOperacion ErrorDe(Troceo troceo, IReadOnlyList<string> cabecera) => troceo switch
    {
        Troceo.FinDeLineaNoAdmitido => ErroresDeImportacion.FinDeLineaNoAdmitido(),
        Troceo.ComillasSinCerrar => ErroresDeImportacion.ComillasSinCerrar(),

        // Solo la primera fila puede acabar el texto sin haber empezado: es el fichero vacío.
        Troceo.FinDelTexto => ErroresDeImportacion.CabeceraNoValida(cabecera),
        _ => throw new ArgumentOutOfRangeException(nameof(troceo), troceo, "No es un error de fichero."),
    };

    private enum Troceo
    {
        Registro,
        FinDelTexto,
        FinDeLineaNoAdmitido,
        ComillasSinCerrar,
    }

    // Parte el texto en registros según la RFC 4180, con el separador que se le dé. Un registro acaba
    // en CRLF, en LF o al final del texto; dentro de comillas, los saltos de línea son del campo.
    private sealed class Troceador(string texto, char separador)
    {
        private readonly StringBuilder _campo = new();
        private int _posicion;

        public Troceo Siguiente(List<string> campos, HashSet<int> malColocadas)
        {
            campos.Clear();
            malColocadas.Clear();

            if (_posicion >= texto.Length)
            {
                return Troceo.FinDelTexto;
            }

            while (true)
            {
                _campo.Clear();
                bool entreComillas = _posicion < texto.Length && texto[_posicion] == '"';
                bool malColocada = false;

                if (entreComillas && !LeerEntreComillas())
                {
                    return Troceo.ComillasSinCerrar;
                }

                // Lo que queda hasta el separador o el fin de línea. En un campo sin comillas es el
                // campo; detrás de unas comillas que cierran no debería haber nada, y si lo hay, o
                // si hay una comilla suelta, el campo se lee igual —para no perder dónde empieza el
                // siguiente— y se marca.
                while (_posicion < texto.Length)
                {
                    char caracter = texto[_posicion];

                    if (caracter == separador || caracter is '\n' or '\r')
                    {
                        break;
                    }

                    malColocada |= entreComillas || caracter == '"';
                    _campo.Append(caracter);
                    _posicion++;
                }

                campos.Add(_campo.ToString());

                if (malColocada)
                {
                    malColocadas.Add(campos.Count - 1);
                }

                if (_posicion >= texto.Length)
                {
                    return Troceo.Registro;
                }

                char fin = texto[_posicion];

                if (fin == separador)
                {
                    _posicion++;
                    continue;
                }

                if (fin == '\n')
                {
                    _posicion++;
                    return Troceo.Registro;
                }

                if (_posicion + 1 < texto.Length && texto[_posicion + 1] == '\n')
                {
                    _posicion += 2;
                    return Troceo.Registro;
                }

                return Troceo.FinDeLineaNoAdmitido;
            }
        }

        // Empieza en la comilla que abre y acaba detrás de la que cierra. False si no cierra.
        private bool LeerEntreComillas()
        {
            _posicion++;

            while (_posicion < texto.Length)
            {
                char caracter = texto[_posicion++];

                if (caracter != '"')
                {
                    _campo.Append(caracter);
                }
                else if (_posicion < texto.Length && texto[_posicion] == '"')
                {
                    _campo.Append('"');
                    _posicion++;
                }
                else
                {
                    return true;
                }
            }

            return false;
        }
    }
}
