using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Organizacion.Domain.Almacenes;

/// <summary>
/// Almacén de una empresa: el sitio al que apunta cada movimiento de existencias.
/// </summary>
/// <remarks>
/// <para>
/// Su dirección va en campos estructurados (R17) y es opcional, porque un almacén virtual o de
/// tránsito no está en ningún sitio y exigirle una dirección obligaría a inventarla.
/// </para>
/// <para>
/// <b>Es <see cref="IBloqueable"/> por un motivo distinto al de la empresa</b>, y desde el 0.10
/// con el mismo mecanismo. Aquí no es el artículo 32: es que cada movimiento de existencias
/// apunta a su almacén para siempre, así que borrarlo rompería el histórico de valoración, que es
/// irreparable. Compartir el mecanismo tiene una consecuencia que se decidió a conciencia y está
/// en el ADR-0016: un almacén bloqueado deja de verse por los caminos ordinarios, igual que una
/// empresa bloqueada, y leer el almacén de un movimiento histórico exigirá abrir un ámbito
/// declarado cuando llegue el módulo de inventario.
/// </para>
/// </remarks>
public sealed class Almacen : EntidadBase, IDeInquilino, IBloqueable
{
    /// <summary>Tope del código del almacén: cabe en una etiqueta y en un albarán.</summary>
    public const int LongitudMaximaDeCodigo = 20;

    private Almacen(
        Guid id,
        Guid empresaId,
        string codigo,
        string nombre,
        Direccion? direccion,
        TipoDeAlmacen tipo,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        Codigo = codigo;
        Nombre = nombre;
        Direccion = direccion;
        Tipo = tipo;
        Bloqueo = Bloqueo.Ninguno();
    }

    private Almacen()
    {
        Codigo = null!;
        Nombre = null!;
        Bloqueo = null!;
    }

    /// <summary>Identificador del almacén.</summary>
    public Guid Id { get; private set; }

    /// <summary>Empresa a la que pertenece (R8).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Código del almacén, en mayúsculas. No cambia: ya está impreso fuera.</summary>
    public string Codigo { get; private set; }

    /// <summary>Nombre con el que se muestra.</summary>
    public string Nombre { get; private set; }

    /// <summary>Dirección en campos estructurados (R17); nula en los almacenes sin sitio.</summary>
    public Direccion? Direccion { get; private set; }

    /// <summary>Naturaleza del almacén.</summary>
    public TipoDeAlmacen Tipo { get; private set; }

    /// <inheritdoc/>
    public Bloqueo Bloqueo { get; private set; }

    /// <summary>Crea un almacén activo.</summary>
    /// <remarks>El <c>momento</c> es la fecha de creación, y la pone quien tiene el
    /// <c>TimeProvider</c>: no la base de datos.</remarks>
    public static Almacen Crear(
        Guid empresaId,
        string codigo,
        string nombre,
        Direccion? direccion,
        TipoDeAlmacen tipo,
        DateTimeOffset momento)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un almacén pertenece siempre a una empresa (R8).", nameof(empresaId));
        }

        ExigirDireccionCoherenteConElTipo(direccion, tipo);

        return new Almacen(
            Guid.CreateVersion7(),
            empresaId,
            CodigoValido(codigo),
            NombreValido(nombre),
            direccion,
            tipo,
            momento);
    }

    /// <summary>Cambia nombre, dirección y tipo. El código no.</summary>
    public void Modificar(string nombre, Direccion? direccion, TipoDeAlmacen tipo)
    {
        Bloqueo.ExigirQueNoEsteBloqueado(
            $"El almacén {Codigo}, bloqueado,",
            "su histórico de valoración lo señala para siempre y la ficha que lo describe se " +
            "conserva con él");

        ExigirDireccionCoherenteConElTipo(direccion, tipo);

        Nombre = NombreValido(nombre);
        Direccion = direccion;
        Tipo = tipo;
    }

    /// <inheritdoc/>
    /// <remarks>Deja de admitir movimientos, y su histórico se conserva.</remarks>
    public void Bloquear(MotivoDeBloqueo motivo, DateTimeOffset momento) =>
        Bloqueo = Bloqueo.Bloquear(motivo, momento);

    /// <inheritdoc/>
    public void Desbloquear() => Bloqueo = Bloqueo.Desbloquear();

    private static void ExigirDireccionCoherenteConElTipo(Direccion? direccion, TipoDeAlmacen tipo)
    {
        if (tipo == TipoDeAlmacen.Fisico && direccion is null)
        {
            throw new ArgumentException(
                "Un almacén físico tiene dirección: es un sitio al que llega mercancía.",
                nameof(direccion));
        }
    }

    /// <summary>Deja el código en la forma exacta en la que se guarda.</summary>
    /// <remarks>
    /// Pública a propósito: sobre esta forma hay un índice único, y quien comprueba si el código ya
    /// existe ANTES de insertar tiene que preguntar por ella. Preguntando por lo que escribió el
    /// usuario, «fac» pasaría el filtro, chocaría contra el índice y saldría como un 500 en vez de
    /// como un 409 con explicación. No valida nada —de la longitud se encarga la creación—: una
    /// pregunta no tiene por qué reventar.
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

        string normalizado = NormalizarCodigo(codigo);

        return normalizado.Length <= LongitudMaximaDeCodigo
            ? normalizado
            : throw new ArgumentException(
                $"El código de almacén admite {LongitudMaximaDeCodigo} caracteres como máximo.",
                nameof(codigo));
    }

    private static string NombreValido(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        return nombre.Trim();
    }
}
