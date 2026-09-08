using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>
/// Un nodo del árbol con el que una empresa clasifica su catálogo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es de la empresa (R8)</b>, al contrario que las unidades y los impuestos: cómo clasifica su
/// catálogo una ferretería no tiene por qué parecerse a cómo lo clasifica una imprenta, y un árbol
/// compartido obligaría a las dos a ponerse de acuerdo o a llenarlo de ramas ajenas.
/// </para>
/// <para>
/// <b>El árbol es una lista de adyacencia</b> —cada fila guarda su padre y nada más—, y no una
/// tabla de cierre. El motivo está escrito en <c>docs/PLAN.md</c> con su alternativa costeada; el
/// resumen es que lo que este módulo consulta de verdad son <b>ascensos</b> —del artículo a su
/// categoría, y de ahí hacia la raíz— y no descensos por el subárbol, y que una tabla de cierre es
/// una segunda verdad que hay que mantener a mano en cada reasignación de padre.
/// </para>
/// <para>
/// <b>Lo que esta clase NO comprueba: los ciclos.</b> Un ciclo relaciona <b>varias instancias</b>
/// del agregado, y la R12 dice una transacción, un agregado: un invariante de dominio que tuviera
/// que cargar el resto del árbol sería justo la grieta que la R12 cierra. La comprobación vive en
/// la capa de aplicación, en <c>ElArbolSigueSiendoUnArbol</c>, exactamente como
/// <c>LaInversaEsPlausible</c> del ítem 1.7 — el patrón se reutiliza, no se inventa otro.
/// </para>
/// </remarks>
public sealed class Categoria : EntidadBase, IDeInquilino
{
    /// <summary>Tope del código de la categoría: cabe en una etiqueta y en un informe.</summary>
    public const int LongitudMaximaDeCodigo = 20;

    /// <summary>Tope del nombre con el que se muestra.</summary>
    public const int LongitudMaximaDeNombre = 100;

    /// <summary>
    /// Cuántos niveles puede haber entre una categoría y la raíz de su árbol.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La cota existe por dos motivos y solo uno es de negocio.</b> El primero es de negocio: un
    /// árbol de clasificación con más de diez niveles ha dejado de clasificar —nadie navega diez
    /// pliegues—, y lo que suele haber a esa profundidad es un atributo del artículo disfrazado de
    /// categoría, que en la fase 2 tendrá su propio sitio.
    /// </para>
    /// <para>
    /// El segundo es de supervivencia, y es el que la hace obligatoria: la comprobación de ciclos
    /// <b>asciende por los padres</b>, y un ascenso sin cota sobre datos que ya tuvieran un ciclo
    /// no da error, <b>se cuelga</b> — dentro de una petición y con la conexión abierta. Un fallo
    /// que se manifiesta como un servidor que no contesta es peor que cualquier rechazo, porque no
    /// dice de qué venía. Con la cota puesta, ese mismo dato corrupto sale como un error con
    /// nombre y con la cadena de códigos que lo produce escrita en el mensaje.
    /// </para>
    /// <para>
    /// Diez, y no un número grande cualquiera: la cota acota también <b>cuántas consultas</b> hace
    /// el ascenso, una por nivel. Mil niveles serían mil viajes a la base por cada alta.
    /// </para>
    /// </remarks>
    public const int ProfundidadMaxima = 10;

    private Categoria(
        Guid id,
        Guid empresaId,
        string codigo,
        string nombre,
        Guid? padreId,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        Codigo = codigo;
        Nombre = nombre;
        PadreId = padreId;
    }

    private Categoria()
    {
        Codigo = null!;
        Nombre = null!;
    }

    /// <summary>Identificador de la categoría.</summary>
    public Guid Id { get; private set; }

    /// <summary>Empresa a la que pertenece (R8).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Código de la categoría, en mayúsculas. No cambia.</summary>
    public string Codigo { get; private set; }

    /// <summary>Nombre con el que se muestra.</summary>
    public string Nombre { get; private set; }

    /// <summary>
    /// Categoría de la que cuelga, o nula si es una raíz.
    /// </summary>
    /// <remarks>
    /// Anulable, y no un identificador especial de raíz: una raíz artificial obligaría a sembrar
    /// una fila por empresa y a acordarse de excluirla de todos los listados. El nulo dice lo
    /// mismo y no hay que mantenerlo.
    /// </remarks>
    public Guid? PadreId { get; private set; }

    /// <summary>Da de alta una categoría.</summary>
    /// <param name="empresaId">Empresa a la que pertenece (R8), que sale del <i>claim</i>.</param>
    /// <param name="codigo">Código; se normaliza a mayúsculas.</param>
    /// <param name="nombre">Nombre con el que se muestra.</param>
    /// <param name="padreId">Categoría de la que cuelga, o nula para una raíz.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    public static Categoria Crear(
        Guid empresaId,
        string codigo,
        string nombre,
        Guid? padreId,
        DateTimeOffset momento)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una categoría pertenece siempre a una empresa (R8).", nameof(empresaId));
        }

        return new Categoria(
            Guid.CreateVersion7(),
            empresaId,
            CodigoValido(codigo),
            NombreValido(nombre),
            PadreValido(padreId),
            momento);
    }

    /// <summary>Cambia el nombre y de quién cuelga. El código no.</summary>
    /// <remarks>
    /// <b>Reasignar el padre es la operación que puede cerrar un ciclo</b>, y por eso la
    /// comprobación de <c>ElArbolSigueSiendoUnArbol</c> corre también aquí y no solo en el alta:
    /// una hoja recién nacida no cierra nada —su identificador no está todavía en el árbol—, pero
    /// mover una rama debajo de su propia descendencia sí. El caso degenerado —padre igual a sí
    /// misma— tampoco se contesta aquí: es un ciclo de longitud uno y sale con el mismo error con
    /// nombre que los demás, porque para quien lo provoca es el mismo problema.
    /// </remarks>
    /// <param name="nombre">Nombre con el que se muestra.</param>
    /// <param name="padreId">Categoría de la que cuelga, o nula para dejarla como raíz.</param>
    public void Modificar(string nombre, Guid? padreId)
    {
        Nombre = NombreValido(nombre);
        PadreId = PadreValido(padreId);
    }

    /// <summary>Deja el código en la forma exacta en la que se guarda.</summary>
    /// <remarks>
    /// Pública a propósito, por lo mismo que en el almacén: sobre esta forma hay un índice único, y
    /// quien comprueba si el código ya existe ANTES de insertar tiene que preguntar por ella.
    /// Preguntando por lo que escribió el usuario, un código en minúsculas pasaría el filtro,
    /// chocaría contra el índice y saldría como un 500 en vez de como un 409 con explicación.
    /// </remarks>
    /// <param name="codigo">Código tal como lo escribieron.</param>
    public static string NormalizarCodigo(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return codigo.Trim().ToUpperInvariant();
    }

    // Un `Guid.Empty` en el padre no es «sin padre»: es un identificador que no existe, y dejarlo
    // pasar convertiría la fila en una huérfana que ningún ascenso encuentra. Se normaliza a nulo
    // aquí, en un solo sitio, en vez de en cada llamante.
    private static Guid? PadreValido(Guid? padreId) =>
        padreId == Guid.Empty ? null : padreId;

    private static string CodigoValido(string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string normalizado = NormalizarCodigo(codigo);

        return normalizado.Length <= LongitudMaximaDeCodigo
            ? normalizado
            : throw new ArgumentException(
                $"El código de categoría admite {LongitudMaximaDeCodigo} caracteres como máximo.",
                nameof(codigo));
    }

    private static string NombreValido(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        string limpio = nombre.Trim();

        return limpio.Length <= LongitudMaximaDeNombre
            ? limpio
            : throw new ArgumentException(
                $"El nombre de la categoría admite {LongitudMaximaDeNombre} caracteres como máximo.",
                nameof(nombre));
    }
}
