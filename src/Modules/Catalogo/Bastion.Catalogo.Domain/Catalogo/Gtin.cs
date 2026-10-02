namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>
/// Número mundial de artículo comercial de GS1, ya normalizado a catorce dígitos, con su control
/// comprobado y con un prefijo que puede ser de un artículo (ADR-0051 §1 y §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una cadena, no un número.</b> Los ceros de delante de un GTIN-12 son parte del prefijo de
/// empresa U.P.C., y un entero se los comería. Se guarda en catorce, rellenando con ceros por la
/// izquierda, y se compara sobre esa forma: el GTIN-12 y el mismo con un cero delante son el mismo
/// GTIN, y el índice único los ve iguales porque los dos llegan aquí como la misma cadena.
/// </para>
/// <para>
/// <b>Dos puertas, como el <c>Iban</c></b> (ADR-0004): <see cref="Leer"/> para el borde, que necesita
/// saber por qué no es un GTIN para decírselo a quien lo tecleó, y <see cref="De"/> para cuando el
/// valor ya viene comprobado. El dominio no devuelve <c>Resultado</c>, así que la lectura trae el
/// motivo y la aplicación lo convierte en su error.
/// </para>
/// </remarks>
public sealed record Gtin
{
    /// <summary>Cuánto mide un GTIN guardado: el de un GTIN-14, el más largo.</summary>
    public const int Longitud = 14;

    private Gtin(string valor) => Valor = valor;

    /// <summary>Los largos con los que entra un GTIN: GTIN-8, GTIN-12, GTIN-13 y GTIN-14.</summary>
    public static IReadOnlyList<int> LargosAdmitidos { get; } = [8, 12, 13, 14];

    /// <summary>Los catorce dígitos, con los ceros de relleno delante.</summary>
    public string Valor { get; }

    /// <summary>Lee un texto de fuera y dice si es un GTIN de artículo, o por qué no lo es.</summary>
    /// <remarks>
    /// No lanza con nada: es la puerta por donde entra lo que teclea una persona o lee un lector.
    /// </remarks>
    /// <param name="texto">Lo que llegó, con o sin espacios en los extremos.</param>
    public static LecturaDeGtin Leer(string? texto)
    {
        // Se recortan los extremos y nada más (ADR-0051 §1). Un espacio o un guion por dentro no se
        // quitan en silencio: alguien tiene que mirar un código que llega roto. Y solo cifras ASCII,
        // porque `char.IsDigit` daría por buenas las arábigas o las de ancho completo.
        string recortado = texto?.Trim() ?? string.Empty;

        if (!recortado.All(char.IsAsciiDigit))
        {
            return LecturaDeGtin.Rechazada(MotivoDeRechazoDelGtin.NoSonDigitos);
        }

        if (!LargosAdmitidos.Contains(recortado.Length))
        {
            return LecturaDeGtin.Rechazada(MotivoDeRechazoDelGtin.LargoNoAdmitido);
        }

        string catorce = recortado.PadLeft(Longitud, '0');

        if (catorce[^1] - '0' != DigitoDeControl(catorce))
        {
            return LecturaDeGtin.Rechazada(MotivoDeRechazoDelGtin.DigitoDeControl);
        }

        MotivoDeRechazoDelGtin? motivo = LeerPrefijo(catorce);

        return motivo is { } rechazo
            ? LecturaDeGtin.Rechazada(rechazo)
            : LecturaDeGtin.Valida(new Gtin(catorce));
    }

    /// <summary>Construye el GTIN, o lanza si el texto no es el de un artículo.</summary>
    /// <param name="texto">Un GTIN ya comprobado, en cualquiera de sus largos.</param>
    /// <exception cref="ArgumentException">El texto no es un GTIN de artículo.</exception>
    public static Gtin De(string texto)
    {
        LecturaDeGtin lectura = Leer(texto);

        return lectura.EsGtin
            ? lectura.Gtin
            : throw new ArgumentException(
                $"«{texto}» no es el GTIN de un artículo: {lectura.Motivo}.", nameof(texto));
    }

    /// <summary>Los catorce dígitos.</summary>
    public override string ToString() => Valor;

    // §7.9.1: de derecha a izquierda y sin contar el de control, pesos 3 y 1 alternos empezando por
    // 3; el control es lo que falta hasta la decena. Sobre los catorce, porque el relleno con ceros
    // no cambia la suma y así la misma cuenta vale para los cuatro largos.
    private static int DigitoDeControl(string catorce)
    {
        int suma = 0;

        for (int posicion = catorce.Length - 2, peso = 3; posicion >= 0; posicion--, peso = 4 - peso)
        {
            suma += (catorce[posicion] - '0') * peso;
        }

        return (10 - (suma % 10)) % 10;
    }

    // El prefijo se lee en los trece dígitos que siguen al indicador. Si esos trece empiezan por
    // 00000, son un GTIN-8 rellenado, y se leen con la tabla 1-5: la 1-4 reserva del 0000001 al
    // 0000099 justo para que no choquen. Su fila 0000000 son los mismos números que el 000-099 de
    // la 1-5, con el mismo motivo, así que esa tabla los cubre a los dos (ADR-0052 §2).
    private static MotivoDeRechazoDelGtin? LeerPrefijo(string catorce)
    {
        // §2.1.10: un GTIN-14 con indicador 9 es de medida variable, y le falta la medida. Va antes
        // que el prefijo, porque lo que lleva detrás no es un GTIN que se pueda leer por partes.
        if (catorce[0] == '9')
        {
            return MotivoDeRechazoDelGtin.MedidaVariable;
        }

        string trece = catorce[1..];

        return trece.StartsWith("00000", StringComparison.Ordinal)
            ? LeerPrefijoDeOcho(Numero(trece[5..], 3))
            : LeerPrefijoDeTrece(trece);
    }

    // Tabla 1-4 de las GS1 General Specifications, Release 26.0, §1.2.3.1, y las RCN-12 de la
    // §2.1.11.3, que la tabla no enseña. Una línea por fila, para que quitar una al aflojar sea
    // quitar una línea (ADR-0051 §4).
    private static MotivoDeRechazoDelGtin? LeerPrefijoDeTrece(string trece)
    {
        int dos = Numero(trece, 2);
        int tres = Numero(trece, 3);

        if (EsUnLac(trece))
        {
            return MotivoDeRechazoDelGtin.CirculacionRestringida;
        }

        if (EsUnRzsc(trece))
        {
            return MotivoDeRechazoDelGtin.CirculacionRestringida;
        }

        if (dos is 2 or 4)
        {
            return MotivoDeRechazoDelGtin.CirculacionRestringida;
        }

        if (dos is >= 20 and <= 29)
        {
            return MotivoDeRechazoDelGtin.CirculacionRestringida;
        }

        if (tres == 951)
        {
            return MotivoDeRechazoDelGtin.SinAsignar;
        }

        if (tres is >= 980 and <= 983)
        {
            return MotivoDeRechazoDelGtin.Cupon;
        }

        if (tres is >= 984 and <= 989)
        {
            return MotivoDeRechazoDelGtin.SinAsignar;
        }

        return dos == 99 ? MotivoDeRechazoDelGtin.Cupon : null;
    }

    // Tabla 1-5, la de los prefijos GS1-8. El 952 de demostración cae entre el 300 y el 976, y pasa.
    private static MotivoDeRechazoDelGtin? LeerPrefijoDeOcho(int tres)
    {
        if (tres <= 99)
        {
            return MotivoDeRechazoDelGtin.CirculacionRestringida;
        }

        if (tres is >= 200 and <= 299)
        {
            return MotivoDeRechazoDelGtin.CirculacionRestringida;
        }

        return tres >= 977 ? MotivoDeRechazoDelGtin.SinAsignar : null;
    }

    // §2.1.11.3, figura 2-1: los códigos locales (LAC) que una empresa imprime en UPC-E para su uso
    // interno. Son GTIN-12 del U.P.C. 001000 al 007999, con cuatro ceros detrás y un 5 a 9 en la
    // undécima posición. Las posiciones N1 a N11 son las del GTIN-12, que va en los trece detrás de
    // un cero; la duodécima es el control, que la figura no limita.
    private static bool EsUnLac(string trece) =>
        trece.StartsWith("000", StringComparison.Ordinal)
        && trece[3] is >= '1' and <= '7'
        && trece[7..11] == "0000"
        && trece[11] is >= '5' and <= '9';

    // La misma figura: los códigos de supresión de ceros de la tienda (RZSC). U.P.C. 001000 a
    // 005000, cinco ceros detrás y un 1 a 9 en la novena posición.
    private static bool EsUnRzsc(string trece) =>
        trece.StartsWith("000", StringComparison.Ordinal)
        && trece[3] is >= '1' and <= '5'
        && trece[4..9] == "00000"
        && trece[9] is >= '1' and <= '9';

    private static int Numero(string digitos, int cuantos)
    {
        int numero = 0;

        foreach (char digito in digitos[..cuantos])
        {
            numero = (numero * 10) + (digito - '0');
        }

        return numero;
    }
}
