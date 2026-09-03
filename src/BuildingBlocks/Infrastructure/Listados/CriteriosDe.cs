using System.Linq.Expressions;

namespace Bastion.BuildingBlocks.Infrastructure.Listados;

/// <summary>
/// Lo que un recurso deja pedir de su listado: por qué campos se puede ordenar y qué mira el
/// filtro de texto.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta es LA lista, no una copia de ella.</b> Los nombres que el borde acepta en
/// <c>?sort=</c> son las claves de <see cref="Ordenables"/>, leídas de aquí y no escritas por
/// segunda vez en el controlador. Una lista de nombres permitidos y un mapa de nombre a columna,
/// mantenidos aparte, divergen el día que alguien renombra un campo en uno de los dos: la URL
/// sigue aceptándose y el orden que sale es otro, o revienta en ejecución. Aquí no hay dos listas
/// que puedan divergir porque solo hay una.
/// </para>
/// <para>
/// <b>El tope no es una cortesía.</b> Sin él, <c>?sort=</c> ordena por cualquier columna que
/// exista, incluida una sin índice, y eso es un recorrido completo de la tabla que escribe
/// cualquiera desde la barra del navegador — el mismo agujero que <c>?size=100000</c>, por otra
/// puerta.
/// </para>
/// <para>
/// <b>El filtro de la URL solo mira campos que no son sensibles</b>, y eso lo decide cada
/// repositorio al escribir su <see cref="Filtro"/>. Buscar por NIF, correo o teléfono va por
/// cuerpo con <c>POST .../buscar</c> (ADR-0025), porque una cadena de consulta se copia, se
/// comparte y se escribe entera en el registro de acceso del servidor de delante.
/// </para>
/// </remarks>
/// <typeparam name="TEntidad">La entidad que se lista.</typeparam>
public sealed class CriteriosDe<TEntidad>
    where TEntidad : class
{
    /// <summary>
    /// Nombre externo de cada campo por el que se puede ordenar, y por dónde ordena.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Son <see cref="LambdaExpression"/> y no <c>Expression&lt;Func&lt;TEntidad, object&gt;&gt;</c>
    /// a propósito. Con <c>object</c>, el compilador mete un <c>Convert</c> al construir cada
    /// lambda, y un campo que va con conversor de valor —un correo, un NIF— deja de traducirse:
    /// la consulta compila y revienta en ejecución con «no se pudo traducir la expresión».
    /// Guardadas sin cerrar, cada una conserva el tipo real de su clave y el <c>OrderBy</c> se
    /// cierra sobre él.
    /// </para>
    /// <para>
    /// El precio es el molde explícito en cada línea del diccionario: una lambda no tiene tipo
    /// natural, así que no se convierte sola a <see cref="LambdaExpression"/>. Se paga a gusto —
    /// el molde es justo lo que deja escrito, en el sitio, el tipo real por el que se ordena.
    /// </para>
    /// </remarks>
    public required IReadOnlyDictionary<string, LambdaExpression> Ordenables { get; init; }

    /// <summary>Por qué campo se ordena cuando el cliente no pide ninguno.</summary>
    /// <remarks>
    /// Tiene que ser una clave de <see cref="Ordenables"/>, y lo comprueba
    /// <see cref="Paginador"/> al usarlo. Sin orden por omisión, PostgreSQL no promete ninguno
    /// entre consultas: la página 2 puede repetir o saltarse filas de la 1 sin que nadie haya
    /// tocado nada.
    /// </remarks>
    public required string PorOmision { get; init; }

    /// <summary>Si el orden de omisión va de mayor a menor.</summary>
    /// <remarks>
    /// Lo necesitan los listados cuya pregunta natural mira hacia atrás: quien abre las
    /// cotizaciones busca la de hoy, no la de hace ocho años.
    /// </remarks>
    public bool DescendentePorOmision { get; init; }

    /// <summary>
    /// Lo que va DETRÁS del campo pedido, hasta llegar a un orden único.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Termina siempre en el identificador, y eso no es ceremonia: ordenar por un campo con
    /// repetidos deja el orden de los empatados a merced del plan de ejecución, y eso es otra vez
    /// la página 2 repitiendo filas de la 1. El identificador desempata siempre porque es único
    /// por construcción.
    /// </para>
    /// <para>
    /// Es una cadena y no una sola clave porque hay listados cuyo orden natural son varias
    /// columnas —un impuesto se lee por código y, dentro del código, por vigencia más reciente—.
    /// Si el cliente pide precisamente una de esas columnas, sale repetida en el <c>ORDER BY</c>;
    /// es inocuo, y el precio de no tener dos formas de declarar un orden.
    /// </para>
    /// </remarks>
    public required Func<IOrderedQueryable<TEntidad>, IOrderedQueryable<TEntidad>> Desempate { get; init; }

    /// <summary>
    /// Qué significa el <c>?q=</c> de este recurso, o nulo si no admite filtro de texto.
    /// </summary>
    /// <remarks>
    /// Recibe el texto ya recortado y no vacío; devolver una condición traducible es cosa del
    /// repositorio, que es quien conoce EF Core y quien sabe qué columnas tienen índice.
    /// </remarks>
    public Func<string, Expression<Func<TEntidad, bool>>>? Filtro { get; init; }

    /// <summary>Los nombres que el borde acepta en <c>?sort=</c>.</summary>
    /// <remarks>
    /// Se calcula al leerla, sin guardarla: son dos o tres nombres, y guardarla obligaría a un
    /// campo de respaldo que el analizador —con razón— lee como una propiedad automática escrita
    /// a mano.
    /// </remarks>
    public IReadOnlySet<string> CamposOrdenables => Ordenables.Keys.ToHashSet(StringComparer.Ordinal);
}
