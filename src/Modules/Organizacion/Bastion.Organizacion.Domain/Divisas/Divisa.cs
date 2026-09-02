using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Entidades;

namespace Bastion.Organizacion.Domain.Divisas;

/// <summary>
/// Una divisa con la que esta instalación opera: su código ISO 4217 y cómo se llama.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí hay dos «Divisa» y no son la misma cosa</b>, y confundirlas es el riesgo que este
/// comentario existe para cerrar. Una es <see cref="CatalogoDeDivisas"/>, un catálogo estático de
/// los bloques comunes que dice qué es un código con forma ISO 4217 y <b>con cuántos decimales se
/// redondea</b> cada uno; se llamaba <c>Divisas</c> y hubo que renombrarlo justo por esto, porque
/// esta carpeta lo tapaba entero. La otra es esta entidad: la lista, en la base de datos, de las
/// divisas que esta instalación usa de verdad, con el nombre que se enseña.
/// </para>
/// <para>
/// <b>El redondeo NO se guarda en la tabla, y esa es la decisión.</b> Cuántos decimales tiene una
/// divisa es una regla fiscal —el yen no tiene ninguno, el dinar tiene tres—, no una preferencia:
/// puesta en una columna, cualquiera puede escribir un 3 en el euro y la facturación entera
/// empieza a redondear mal sin que nada falle. Se queda donde estaba, en código y con su caso
/// dorado, y esta entidad lo consulta. Además
/// <see cref="Bastion.BuildingBlocks.Domain.Dinero.Importe"/> es un objeto de valor del dominio y
/// redondea sin poder abrir una conexión: si la autoridad fuera la tabla, no podría preguntársela.
/// </para>
/// <para>
/// <b>Y las dos no pueden separarse</b>, que es lo que pasa siempre con dos listas de lo mismo:
/// <see cref="Crear"/> exige que la divisa esté en el catálogo de redondeo, así que una fila cuyo
/// redondeo no se conozca <b>no se puede insertar</b>. Dar de alta una divisa nueva pasa por
/// añadirla antes al catálogo con su caso dorado, que es exactamente donde se quiere que alguien
/// se pare a pensar.
/// </para>
/// <para>
/// Es un maestro de la instalación y no de una empresa (R8): el euro es el euro para todas las
/// sociedades. Cuál usa cada una como base es un campo <b>de la empresa</b>, y ese sí filtra.
/// </para>
/// </remarks>
public sealed class Divisa : EntidadBase
{
    /// <summary>Tope del nombre con el que se muestra.</summary>
    public const int LongitudMaximaDeNombre = 60;

    private Divisa(Guid id, string codigo, string nombre, DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        Codigo = codigo;
        Nombre = nombre;
    }

    private Divisa()
    {
        Codigo = null!;
        Nombre = null!;
    }

    /// <summary>Identificador de la divisa.</summary>
    public Guid Id { get; private set; }

    /// <summary>Código ISO 4217 en mayúsculas: <c>EUR</c>, <c>USD</c>.</summary>
    public string Codigo { get; private set; }

    /// <summary>Nombre con el que se muestra.</summary>
    public string Nombre { get; private set; }

    /// <summary>
    /// Decimales de redondeo fiscal, leídos del catálogo de los bloques comunes.
    /// </summary>
    /// <remarks>
    /// Calculado, nunca guardado: ver la explicación de la clase. No puede lanzar sobre una fila
    /// que exista, porque <see cref="Crear"/> no deja nacer ninguna cuyo redondeo no se conozca.
    /// </remarks>
    public int Decimales => CatalogoDeDivisas.UnidadMinima(Codigo);

    /// <summary>Da de alta una divisa con la que operar.</summary>
    /// <param name="codigo">Código ISO 4217; se normaliza a mayúsculas.</param>
    /// <param name="nombre">Nombre con el que se muestra.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    public static Divisa Crear(string codigo, string nombre, DateTimeOffset momento) =>
        new(Guid.CreateVersion7(), CodigoValido(codigo), NombreValido(nombre), momento);

    /// <summary>Cambia el nombre. El código no: es el identificador natural de la fila.</summary>
    /// <param name="nombre">Nombre con el que se muestra.</param>
    public void Modificar(string nombre) => Nombre = NombreValido(nombre);

    /// <summary>Deja el código en la forma exacta en la que se guarda.</summary>
    /// <remarks>
    /// No valida: quien pregunta si un código ya existe antes de insertar necesita preguntar por
    /// la forma guardada, y una pregunta no tiene por qué reventar.
    /// </remarks>
    /// <param name="codigo">Código tal como lo escribieron.</param>
    public static string NormalizarCodigo(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return codigo.Trim().ToUpperInvariant();
    }

    private static string CodigoValido(string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        // `Normalizar` comprueba la FORMA y lanza si no es ISO 4217. Lo que sigue comprueba que
        // además se sabe redondearla, que es lo que impide que esta tabla y el catálogo de los
        // bloques comunes acaben diciendo cosas distintas.
        string normalizado = CatalogoDeDivisas.Normalizar(codigo);

        return CatalogoDeDivisas.EsConocida(normalizado)
            ? normalizado
            : throw new ArgumentException(
                $"No se conoce el redondeo fiscal de {normalizado}. Añádala primero al catálogo " +
                "de divisas de los bloques comunes, con su caso dorado: una divisa que se guarda " +
                "sin saber con cuántos decimales redondea es una factura mal calculada esperando.",
                nameof(codigo));
    }

    private static string NombreValido(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        string limpio = nombre.Trim();

        return limpio.Length <= LongitudMaximaDeNombre
            ? limpio
            : throw new ArgumentException(
                $"El nombre de la divisa admite {LongitudMaximaDeNombre} caracteres como máximo.",
                nameof(nombre));
    }
}
