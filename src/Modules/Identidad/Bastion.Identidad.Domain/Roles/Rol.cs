using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Entidades;

namespace Bastion.Identidad.Domain.Roles;

/// <summary>
/// Un rol: un nombre y el conjunto de permisos que concede (§11).
/// </summary>
/// <remarks>
/// <para>
/// <b>Un rol no es un poder, es una etiqueta para un conjunto de permisos.</b> La autorización
/// nunca pregunta por el rol: pregunta por el permiso. Así la segregación de funciones del §11
/// —quien crea un pedido de compra no lo aprueba, quien registra un pago no lo concilia— se puede
/// expresar quitando un permiso, sin inventar un rol nuevo por cada matiz. Y así no aparece el
/// <c>if (rol == "admin")</c> que convierte a un rol en una excepción a todas las reglas.
/// </para>
/// <para>
/// <b>Los roles son globales; la ASIGNACIÓN es por empresa</b> (<see cref="Usuarios.Membresia"/>).
/// El catálogo de puestos de una pyme —quien contabiliza, quien factura, quien consulta— es el
/// mismo se mire desde la sociedad que se mire; lo que cambia por empresa es quién ocupa cuál.
/// </para>
/// <para>
/// <b>El rol comprueba la FORMA del permiso; que exista lo comprueba el caso de uso.</b> El
/// dominio no puede saber qué permisos declara cada módulo —tendría que referenciarlos a todos—,
/// así que aquí se garantiza que lo concedido es un <see cref="Permiso"/> bien formado y el caso
/// de uso lo contrasta contra <c>ICatalogoDePermisos</c>. Un permiso mal escrito que llegara a
/// la tabla no rompería nada visible: sería una puerta que nunca se abre.
/// </para>
/// </remarks>
public sealed class Rol : EntidadBase
{
    /// <summary>Longitud máxima del código, que es lo que va en la columna.</summary>
    public const int LongitudDelCodigo = 40;

    private readonly List<PermisoDeRol> _permisos = [];

    private Rol()
    {
        Codigo = null!;
        Nombre = null!;
    }

    private Rol(Guid id, string codigo, string nombre, bool esDelSistema, DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        Codigo = codigo;
        Nombre = nombre;
        EsDelSistema = esDelSistema;
    }

    /// <summary>Identificador del rol.</summary>
    public Guid Id { get; private set; }

    /// <summary>Código estable, en minúsculas y con guiones. Es contrato con la semilla.</summary>
    public string Codigo { get; private set; }

    /// <summary>Nombre para la interfaz.</summary>
    public string Nombre { get; private set; }

    /// <summary>
    /// Si lo creó la semilla de arranque y no se puede suprimir.
    /// </summary>
    /// <remarks>
    /// Existe para que nadie pueda dejar la instalación sin ningún rol capaz de conceder
    /// permisos: sin esta marca, borrar el rol de administración es una operación de un clic que
    /// no se puede deshacer desde dentro, porque para deshacerla haría falta el permiso que se
    /// acaba de perder.
    /// </remarks>
    public bool EsDelSistema { get; private set; }

    /// <summary>Permisos que concede.</summary>
    public IReadOnlyCollection<PermisoDeRol> Permisos => _permisos;

    /// <summary>Crea un rol.</summary>
    /// <param name="codigo">Código estable, en minúsculas y con guiones.</param>
    /// <param name="nombre">Nombre para la interfaz.</param>
    /// <param name="momento">Ahora: la fecha de creación, que no pone la base de datos.</param>
    /// <param name="esDelSistema">Si lo crea la semilla y no se puede suprimir.</param>
    public static Rol Crear(
        string codigo, string nombre, DateTimeOffset momento, bool esDelSistema = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        string normalizado = NormalizarCodigo(codigo);

        if (!EsCodigoValido(normalizado))
        {
            throw new ArgumentException(
                $"«{codigo}» no es un código de rol: van en minúsculas, con guiones y hasta " +
                $"{LongitudDelCodigo} posiciones.",
                nameof(codigo));
        }

        return new Rol(Guid.CreateVersion7(), normalizado, nombre.Trim(), esDelSistema, momento);
    }

    /// <summary>Cambia el nombre para la interfaz. El código no se toca: es contrato.</summary>
    /// <param name="nombre">Nombre nuevo.</param>
    public void Renombrar(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        Nombre = nombre.Trim();
    }

    /// <summary>Concede un permiso. Repetirlo no hace nada.</summary>
    /// <param name="permiso">Permiso bien formado.</param>
    /// <returns>Si no lo tenía ya.</returns>
    public bool Conceder(Permiso permiso)
    {
        ArgumentNullException.ThrowIfNull(permiso);

        if (Tiene(permiso))
        {
            return false;
        }

        _permisos.Add(new PermisoDeRol(Id, permiso.Valor));
        return true;
    }

    /// <summary>Retira un permiso. Retirar uno que no tenía no hace nada.</summary>
    /// <param name="permiso">Permiso que se retira.</param>
    /// <returns>Si lo tenía.</returns>
    public bool Retirar(Permiso permiso)
    {
        ArgumentNullException.ThrowIfNull(permiso);

        return _permisos.RemoveAll(concedido => concedido.Permiso == permiso.Valor) > 0;
    }

    /// <summary>Si concede ese permiso.</summary>
    /// <param name="permiso">Permiso que se busca.</param>
    public bool Tiene(Permiso permiso)
    {
        ArgumentNullException.ThrowIfNull(permiso);

        return _permisos.Exists(concedido => concedido.Permiso == permiso.Valor);
    }

    /// <summary>Deja el rol con exactamente esos permisos, ni uno más.</summary>
    /// <remarks>
    /// Es lo que necesita un formulario que edita la lista entera: con conceder y retirar sueltos,
    /// el borde tendría que calcular la diferencia, y ese cálculo escrito en el sitio equivocado
    /// es como se quedan permisos concedidos que el formulario ya no mostraba.
    /// </remarks>
    /// <param name="permisos">Los permisos que el rol debe conceder.</param>
    public void FijarPermisos(IEnumerable<Permiso> permisos)
    {
        ArgumentNullException.ThrowIfNull(permisos);

        _permisos.Clear();

        foreach (Permiso permiso in permisos)
        {
            Conceder(permiso);
        }
    }

    /// <summary>Recorta y pasa a minúsculas, que es como se guarda.</summary>
    /// <param name="codigo">Código tal como se haya escrito.</param>
    public static string NormalizarCodigo(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return codigo.Trim().ToLowerInvariant();
    }

    private static bool EsCodigoValido(string codigo)
    {
        if (codigo.Length is 0 or > LongitudDelCodigo || codigo[0] == '-' || codigo[^1] == '-')
        {
            return false;
        }

        foreach (char caracter in codigo)
        {
            if (caracter != '-' && !char.IsAsciiLetterLower(caracter) && !char.IsAsciiDigit(caracter))
            {
                return false;
            }
        }

        return true;
    }
}
