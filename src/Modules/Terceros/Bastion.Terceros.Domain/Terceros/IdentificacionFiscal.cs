using Bastion.BuildingBlocks.Domain.Identificacion;

namespace Bastion.Terceros.Domain.Terceros;

/// <summary>
/// Con qué identificador fiscal se conoce a un tercero: el país que lo emite, el número, y cuánto
/// se sabe de que ese número sea el que dice ser.
/// </summary>
/// <remarks>
/// <para>
/// <b>Son tres campos y no uno porque son dos mundos.</b> Dentro de España el identificador se
/// valida —<see cref="Nif"/> comprueba el carácter de control del DNI, del NIE y del CIF—, y
/// fuera no hay nada que validar: cada país tiene su forma y no existe un algoritmo común. La
/// tentación es guardar los dos en la misma columna y no decir nada; lo que eso produce es un
/// maestro en el que la mitad de las fichas parecen comprobadas sin serlo.
/// </para>
/// <para>
/// <b>El país no es adorno: es parte de la identidad.</b> Un número de nueve caracteres puede ser
/// un NIF español y, con las mismas cifras, el identificador de otra cosa en Portugal. Sin el
/// país, la unicidad por (empresa, identificador) haría chocar dos terceros que no tienen nada
/// que ver, y quien diera de alta al segundo recibiría un conflicto incomprensible.
/// </para>
/// <para>
/// <b>Y por eso «ES» no se puede escribir por la puerta de atrás.</b> <see cref="Extranjera"/>
/// rechaza España: si se admitiera, cualquiera podría meter un NIF con la letra mal diciendo que
/// es extranjero, y el criterio de este ítem —«NIF, NIE o CIF validados de verdad»— se caería sin
/// que nada se pusiera rojo. Para España hay una sola puerta y valida.
/// </para>
/// </remarks>
public sealed record IdentificacionFiscal
{
    /// <summary>España, en ISO 3166-1 alfa-2. El único país con validación propia.</summary>
    public const string PaisDeEspana = "ES";

    /// <summary>Longitud del país: ISO 3166-1 alfa-2, ni una letra más ni una menos.</summary>
    public const int LongitudDelPais = 2;

    /// <summary>
    /// Tope del número. Da de sobra para el más largo de la Unión —un número de IVA con su
    /// prefijo— y no tanto como para que la columna deje de decir qué cabe en ella.
    /// </summary>
    public const int LongitudMaximaDelNumero = 20;

    private IdentificacionFiscal(string pais, string numero, EstadoDeVerificacion verificacion)
    {
        Pais = pais;
        Numero = numero;
        Verificacion = verificacion;
    }

    /// <summary>País que emite el identificador, en ISO 3166-1 alfa-2 y en mayúsculas.</summary>
    public string Pais { get; }

    /// <summary>El identificador, normalizado: en mayúsculas y sin espacios ni puntuación.</summary>
    public string Numero { get; }

    /// <summary>Cuánto se ha comprobado de este identificador.</summary>
    public EstadoDeVerificacion Verificacion { get; }

    /// <summary>Si es un identificador español, y por tanto uno que se ha podido validar.</summary>
    public bool EsEspanola => Pais == PaisDeEspana;

    /// <summary>La identidad de un tercero español, a partir de un NIF ya validado.</summary>
    /// <remarks>
    /// Recibe un <see cref="Nif"/> y no una cadena a propósito: el tipo es la prueba de que la
    /// validación ocurrió. Con una cadena, esta fábrica tendría que validar otra vez o fiarse, y
    /// la versión que se fía compila igual de bien.
    /// </remarks>
    /// <param name="nif">NIF, NIE o CIF ya construido, o sea con su carácter de control cuadrado.</param>
    public static IdentificacionFiscal Espanola(Nif nif)
    {
        ArgumentNullException.ThrowIfNull(nif);

        return new IdentificacionFiscal(
            PaisDeEspana, nif.Valor, EstadoDeVerificacion.VerificadoPorAlgoritmo);
    }

    /// <summary>La identidad de un tercero extranjero: opaca, con su país, y sin verificar.</summary>
    /// <remarks>
    /// <b>Nace <see cref="EstadoDeVerificacion.NoVerificado"/> y no hay forma de pedir otra cosa.</b>
    /// El estado no es un parámetro porque no es una opinión de quien da el alta: es lo que se
    /// sabe. El día que exista una consulta al VIES, será ella la que pueda subirlo, y entonces se
    /// verá quién lo hizo y cuándo.
    /// </remarks>
    /// <param name="pais">País emisor, en ISO 3166-1 alfa-2. España no vale por aquí.</param>
    /// <param name="numero">El identificador tal como lo escribieron.</param>
    public static IdentificacionFiscal Extranjera(string pais, string numero)
    {
        string codigo = PaisValido(pais);

        if (codigo == PaisDeEspana)
        {
            throw new ArgumentException(
                "Un identificador español se da de alta como NIF, que se valida. Por esta puerta " +
                "entra lo que no se puede validar, y un NIF sí se puede.",
                nameof(pais));
        }

        return new IdentificacionFiscal(
            codigo, NumeroValido(numero), EstadoDeVerificacion.NoVerificado);
    }

    /// <summary>
    /// El país en la forma exacta en la que se guarda, o <c>null</c> si no es un ISO 3166-1
    /// alfa-2.
    /// </summary>
    /// <remarks>
    /// La <b>puerta que no lanza</b> del par que pide el ADR-0004, y existe por lo mismo que
    /// <c>Nif.Intentar</c>: el borde recibe lo que escribió una persona y tiene que contestar un
    /// error <b>por campo</b>, no una excepción. Devuelve el código normalizado en vez de un
    /// booleano para que quien pregunta no tenga que normalizar otra vez y arriesgarse a hacerlo
    /// distinto.
    /// </remarks>
    /// <param name="pais">El país tal como lo escribieron.</param>
    public static string? PaisNormalizado(string? pais)
    {
        if (string.IsNullOrWhiteSpace(pais))
        {
            return null;
        }

        string codigo = pais.Trim().ToUpperInvariant();

        return codigo.Length == LongitudDelPais && codigo.All(char.IsAsciiLetterUpper)
            ? codigo
            : null;
    }

    /// <summary>
    /// El identificador en la forma exacta en la que se guarda, o <c>null</c> si lo que llega no
    /// deja nada utilizable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pública por lo mismo que <c>Almacen.NormalizarCodigo</c>: sobre esta forma hay un índice
    /// único, y quien pregunte si el identificador ya existe ANTES de insertar tiene que preguntar
    /// por ella. Preguntando por lo que escribió el usuario, «fr 123 456» pasaría el filtro,
    /// chocaría contra el índice y saldría como un 500 en vez de como el conflicto que es.
    /// </para>
    /// <para>
    /// Devuelve <c>null</c> —y no la cadena vacía— cuando no queda ni una letra ni un dígito, que
    /// es lo que pasa con «---» o con un espacio. Buscar por la cadena vacía no es buscar: es
    /// pedir la primera ficha que haya, y el conflicto que produciría al dar de alta sería
    /// incomprensible.
    /// </para>
    /// </remarks>
    /// <param name="numero">El identificador tal como lo escribieron.</param>
    public static string? NumeroNormalizado(string? numero)
    {
        if (numero is null)
        {
            return null;
        }

        // Mismo criterio que `Nif.Normalizar`: espacios, puntos y guiones son ruido de teclado.
        // `ToUpperInvariant` y no `ToUpper()` porque en una máquina con cultura turca la «i» se
        // convertiría en «İ» y dos altas del mismo identificador dejarían de coincidir.
        string normalizado = string.Concat(numero.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();

        return normalizado.Length is > 0 and <= LongitudMaximaDelNumero ? normalizado : null;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Pais}:{Numero}";

    private static string PaisValido(string pais) =>
        PaisNormalizado(pais)
        ?? throw new ArgumentException(
            $"El país se guarda en ISO 3166-1 alfa-2 (dos letras): «{pais}» no lo es.",
            nameof(pais));

    private static string NumeroValido(string numero) =>
        NumeroNormalizado(numero)
        ?? throw new ArgumentException(
            "El identificador no tiene ni una letra ni un dígito, o pasa de " +
            $"{LongitudMaximaDelNumero} caracteres.",
            nameof(numero));
}
