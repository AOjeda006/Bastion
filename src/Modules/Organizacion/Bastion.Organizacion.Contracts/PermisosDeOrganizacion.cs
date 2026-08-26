namespace Bastion.Organizacion.Contracts;

/// <summary>
/// Los permisos que declara el módulo Organización, uno por <b>tipo × verbo</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué están enumerados tipo por tipo y verbo por verbo.</b> Autorizar una operación no
/// autoriza lo que esa operación escribe. <c>crear</c> y <c>modificar</c> son permisos distintos
/// aunque los sirva el mismo controlador y los escriba el mismo repositorio: quien da de alta
/// almacenes no tiene por qué poder cambiar los que ya existen, y quien consulta un ejercicio no
/// tiene por qué poder cerrarlo. Un permiso por recurso —<c>organizacion.almacen</c> a secas—
/// convierte «puede ver» en «puede todo» sin que nadie lo decida.
/// </para>
/// <para>
/// <b>Por qué viven en <c>Contracts</c>.</b> Es lo único de un módulo que otro puede ver (§4), y
/// aquí lo que hace falta ver es exactamente esto: el host los junta con los de los demás módulos
/// para componer el catálogo, e Identidad valida contra ese catálogo los permisos que concede un
/// rol. Si vivieran en Identidad, Identidad tendría que referenciar a los dieciséis módulos.
/// </para>
/// <para>
/// <b>Son constantes y no un tipo.</b> <c>Contracts</c> no referencia nada, ni siquiera los
/// bloques comunes, así que aquí no se puede usar <c>Permiso</c>. La forma se comprueba al
/// componer el catálogo: una constante mal escrita <b>tumba el arranque</b>, que es el único
/// momento en que un permiso mal escrito se puede notar; después sería una puerta que nunca abre.
/// </para>
/// </remarks>
public static class PermisosDeOrganizacion
{
    /// <summary>Consultar empresas.</summary>
    public const string EmpresaVer = "organizacion.empresa.ver";

    /// <summary>Dar de alta empresas.</summary>
    public const string EmpresaCrear = "organizacion.empresa.crear";

    /// <summary>Cambiar los datos de una empresa.</summary>
    public const string EmpresaModificar = "organizacion.empresa.modificar";

    /// <summary>Dar de baja una empresa (R16).</summary>
    public const string EmpresaBloquear = "organizacion.empresa.bloquear";

    /// <summary>Deshacer la baja de una empresa.</summary>
    /// <remarks>
    /// Separado de <see cref="EmpresaBloquear"/> a propósito: quien puede dar de baja no tiene por
    /// qué poder deshacerlo. Deshacer un bloqueo legal es la operación que hay que poder auditar.
    /// </remarks>
    public const string EmpresaDesbloquear = "organizacion.empresa.desbloquear";

    /// <summary>Consultar ejercicios.</summary>
    public const string EjercicioVer = "organizacion.ejercicio.ver";

    /// <summary>Abrir un ejercicio.</summary>
    public const string EjercicioCrear = "organizacion.ejercicio.crear";

    /// <summary>Cambiar las fechas de un ejercicio abierto.</summary>
    public const string EjercicioModificar = "organizacion.ejercicio.modificar";

    /// <summary>Borrar un ejercicio que todavía no tiene series.</summary>
    public const string EjercicioEliminar = "organizacion.ejercicio.eliminar";

    /// <summary>Cerrar un ejercicio (R9).</summary>
    public const string EjercicioCerrar = "organizacion.ejercicio.cerrar";

    /// <summary>Reabrir un ejercicio cerrado.</summary>
    /// <remarks>
    /// El permiso más delicado del módulo: reabrir un ejercicio deja volver a registrar en un
    /// periodo que se dio por cerrado. Va aparte de <see cref="EjercicioCerrar"/> por la misma
    /// razón que la segregación de funciones del §11.
    /// </remarks>
    public const string EjercicioReabrir = "organizacion.ejercicio.reabrir";

    /// <summary>Consultar series.</summary>
    public const string SerieVer = "organizacion.serie.ver";

    /// <summary>Crear una serie de numeración.</summary>
    public const string SerieCrear = "organizacion.serie.crear";

    /// <summary>Cambiar el formato de una serie activa.</summary>
    public const string SerieModificar = "organizacion.serie.modificar";

    /// <summary>Borrar una serie que todavía no ha numerado nada.</summary>
    public const string SerieEliminar = "organizacion.serie.eliminar";

    /// <summary>Consultar almacenes.</summary>
    public const string AlmacenVer = "organizacion.almacen.ver";

    /// <summary>Dar de alta almacenes.</summary>
    public const string AlmacenCrear = "organizacion.almacen.crear";

    /// <summary>Cambiar los datos de un almacén.</summary>
    public const string AlmacenModificar = "organizacion.almacen.modificar";

    /// <summary>Dar de baja un almacén (R16).</summary>
    public const string AlmacenBloquear = "organizacion.almacen.bloquear";

    /// <summary>Deshacer la baja de un almacén.</summary>
    public const string AlmacenDesbloquear = "organizacion.almacen.desbloquear";

    /// <summary>Todos los permisos del módulo, que es lo que el host junta en el catálogo.</summary>
    /// <remarks>
    /// La lista se escribe a mano y no por reflexión sobre las constantes. Por reflexión, quitar
    /// una constante y dejar el endpoint que la exigía no rompería nada aquí; a mano, la lista y
    /// las constantes se leen juntas y una ausencia se ve. Que ninguna se quede fuera lo comprueba
    /// un test.
    /// </remarks>
    public static IReadOnlyList<string> Todos { get; } =
    [
        EmpresaVer,
        EmpresaCrear,
        EmpresaModificar,
        EmpresaBloquear,
        EmpresaDesbloquear,
        EjercicioVer,
        EjercicioCrear,
        EjercicioModificar,
        EjercicioEliminar,
        EjercicioCerrar,
        EjercicioReabrir,
        SerieVer,
        SerieCrear,
        SerieModificar,
        SerieEliminar,
        AlmacenVer,
        AlmacenCrear,
        AlmacenModificar,
        AlmacenBloquear,
        AlmacenDesbloquear,
    ];
}
