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

    /// <summary>Consultar tipos impositivos.</summary>
    public const string ImpuestoVer = "organizacion.impuesto.ver";

    /// <summary>Dar de alta un tramo de un tipo impositivo.</summary>
    public const string ImpuestoCrear = "organizacion.impuesto.crear";

    /// <summary>Cambiar el nombre o las cuentas contables de un tramo.</summary>
    /// <remarks>
    /// No incluye el porcentaje ni las fechas: un impuesto no se edita, se sucede. Subir el IVA
    /// del 18 % al 21 % es cerrar un tramo y abrir otro, y por eso lo hacen dos permisos
    /// distintos de este.
    /// </remarks>
    public const string ImpuestoModificar = "organizacion.impuesto.modificar";

    /// <summary>Poner fecha de fin a un tramo vigente.</summary>
    /// <remarks>
    /// Aparte de <see cref="ImpuestoModificar"/> porque cierra un periodo fiscal: a partir del día
    /// siguiente, una factura que use ese código no encuentra tipo y la emisión se para.
    /// </remarks>
    public const string ImpuestoCerrar = "organizacion.impuesto.cerrar";

    /// <summary>Consultar divisas.</summary>
    public const string DivisaVer = "organizacion.divisa.ver";

    /// <summary>Dar de alta una divisa de las que el catálogo sabe redondear.</summary>
    public const string DivisaCrear = "organizacion.divisa.crear";

    /// <summary>Cambiar el nombre de una divisa.</summary>
    public const string DivisaModificar = "organizacion.divisa.modificar";

    /// <summary>Consultar cotizaciones.</summary>
    public const string TipoCambioVer = "organizacion.tipo-cambio.ver";

    /// <summary>Registrar la cotización de un día.</summary>
    public const string TipoCambioCrear = "organizacion.tipo-cambio.crear";

    /// <summary>Rectificar la tasa de una cotización ya registrada.</summary>
    /// <remarks>
    /// Aparte de <see cref="TipoCambioCrear"/> y con motivo: la cotización de un día pasado ya ha
    /// convertido importes, y cambiarla cambia lo que valían. Registrar la de hoy no.
    /// </remarks>
    public const string TipoCambioModificar = "organizacion.tipo-cambio.modificar";

    /// <summary>Consultar unidades de medida.</summary>
    public const string UnidadMedidaVer = "organizacion.unidad-medida.ver";

    /// <summary>Dar de alta una unidad de medida.</summary>
    public const string UnidadMedidaCrear = "organizacion.unidad-medida.crear";

    /// <summary>Cambiar el nombre de una unidad de medida.</summary>
    public const string UnidadMedidaModificar = "organizacion.unidad-medida.modificar";

    /// <summary>Consultar conversiones entre unidades.</summary>
    public const string ConversionUmVer = "organizacion.conversion-um.ver";

    /// <summary>Dar de alta una conversión entre dos unidades.</summary>
    public const string ConversionUmCrear = "organizacion.conversion-um.crear";

    /// <summary>Cambiar el factor de una conversión.</summary>
    public const string ConversionUmModificar = "organizacion.conversion-um.modificar";

    /// <summary>Consultar ubicaciones.</summary>
    public const string UbicacionVer = "organizacion.ubicacion.ver";

    /// <summary>Dar de alta una ubicación dentro de un almacén.</summary>
    public const string UbicacionCrear = "organizacion.ubicacion.crear";

    /// <summary>Cambiar las coordenadas o la descripción de una ubicación.</summary>
    public const string UbicacionModificar = "organizacion.ubicacion.modificar";

    /// <summary>Dar de baja una ubicación (R16).</summary>
    public const string UbicacionBloquear = "organizacion.ubicacion.bloquear";

    /// <summary>Deshacer la baja de una ubicación.</summary>
    public const string UbicacionDesbloquear = "organizacion.ubicacion.desbloquear";


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
        ImpuestoVer,
        ImpuestoCrear,
        ImpuestoModificar,
        ImpuestoCerrar,
        DivisaVer,
        DivisaCrear,
        DivisaModificar,
        TipoCambioVer,
        TipoCambioCrear,
        TipoCambioModificar,
        UnidadMedidaVer,
        UnidadMedidaCrear,
        UnidadMedidaModificar,
        ConversionUmVer,
        ConversionUmCrear,
        ConversionUmModificar,
        UbicacionVer,
        UbicacionCrear,
        UbicacionModificar,
        UbicacionBloquear,
        UbicacionDesbloquear,
    ];
}
