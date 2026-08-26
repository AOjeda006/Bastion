using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Organizacion.Domain.Almacenes;

/// <summary>
/// Almacén de una empresa: el sitio al que apunta cada movimiento de existencias.
/// </summary>
/// <remarks>
/// Su dirección va en campos estructurados (R17) y es opcional, porque un almacén virtual o de
/// tránsito no está en ningún sitio y exigirle una dirección obligaría a inventarla.
/// </remarks>
public sealed class Almacen : IDeInquilino
{
    /// <summary>Tope del código del almacén: cabe en una etiqueta y en un albarán.</summary>
    public const int LongitudMaximaDeCodigo = 20;

    private Almacen(
        Guid id,
        Guid empresaId,
        string codigo,
        string nombre,
        Direccion? direccion,
        TipoDeAlmacen tipo)
    {
        Id = id;
        EmpresaId = empresaId;
        Codigo = codigo;
        Nombre = nombre;
        Direccion = direccion;
        Tipo = tipo;
        Estado = EstadoDeAlmacen.Activo;
    }

    private Almacen()
    {
        Codigo = null!;
        Nombre = null!;
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

    /// <summary>Activo o bloqueado.</summary>
    public EstadoDeAlmacen Estado { get; private set; }

    /// <summary>Instante del bloqueo, con zona horaria: es un momento, no una fecha de negocio.</summary>
    public DateTimeOffset? BloqueadoEn { get; private set; }

    /// <summary>Crea un almacén activo.</summary>
    public static Almacen Crear(
        Guid empresaId,
        string codigo,
        string nombre,
        Direccion? direccion,
        TipoDeAlmacen tipo)
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
            tipo);
    }

    /// <summary>Cambia nombre, dirección y tipo. El código no.</summary>
    public void Modificar(string nombre, Direccion? direccion, TipoDeAlmacen tipo)
    {
        if (Estado == EstadoDeAlmacen.Bloqueado)
        {
            throw new InvalidOperationException(
                $"El almacén {Codigo} está bloqueado y no admite cambios.");
        }

        ExigirDireccionCoherenteConElTipo(direccion, tipo);

        Nombre = NombreValido(nombre);
        Direccion = direccion;
        Tipo = tipo;
    }

    /// <summary>Bloquea el almacén: deja de admitir movimientos, y su histórico se conserva.</summary>
    public void Bloquear(DateTimeOffset momento)
    {
        if (Estado == EstadoDeAlmacen.Bloqueado)
        {
            return;
        }

        Estado = EstadoDeAlmacen.Bloqueado;
        BloqueadoEn = momento;
    }

    /// <summary>Levanta el bloqueo.</summary>
    public void Desbloquear()
    {
        Estado = EstadoDeAlmacen.Activo;
        BloqueadoEn = null;
    }

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
