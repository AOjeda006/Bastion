namespace Bastion.BuildingBlocks.Application.Multiempresa;

/// <summary>
/// La empresa por la que se filtra <b>esta</b> consulta, y el único sitio del que sale (R8).
/// </summary>
/// <remarks>
/// <para>
/// <b>Falla cerrado.</b> Si no hay empresa activa en el <i>claim</i> y nadie ha abierto un ámbito
/// sin inquilino a propósito, <see cref="EmpresaDelFiltro"/> <b>lanza</b>. No devuelve nulo, no
/// devuelve <see cref="Guid.Empty"/> y no se salta el filtro: cualquiera de esas tres cosas sería
/// un valor por omisión que rellena el hueco y lo esconde, y el síntoma —«no tienes almacenes», o
/// peor, «aquí están los de todos»— no se distingue de un dato correcto.
/// </para>
/// <para>
/// <b>Y hay caminos legítimos sin principal:</b> las migraciones, la semilla de arranque, el login
/// (que busca por correo antes de saber en qué empresa se entra), las comprobaciones de unicidad
/// global —un NIF es único en toda la instalación, no dentro de una empresa— y, desde el 0.8, el
/// trabajo de fondo que publica la bandeja de salida. Para esos, y solo para esos, está
/// <see cref="SinInquilino"/>: un ámbito <b>explícito</b>, con un motivo de una lista cerrada
/// (<see cref="MotivoSinInquilino"/>) y anotado en el registro. Que no lo abra nadie más no se
/// confía: se comprueba en <c>ElFiltroNoSeSaltaPorAhiTests</c>.
/// </para>
/// </remarks>
public interface IInquilinoActual
{
    /// <summary>
    /// La empresa por la que filtrar, o <c>null</c> si hay un ámbito sin inquilino abierto.
    /// </summary>
    /// <remarks>
    /// Es un <b>miembro de instancia que se lee en cada consulta</b>, no un valor que el contexto
    /// copie al construirse: ver el punto 3 del ADR-0011.
    /// </remarks>
    /// <exception cref="FaltaLaEmpresaActivaException">
    /// Si no hay empresa activa en el <i>claim</i> y tampoco hay ámbito sin inquilino abierto.
    /// </exception>
    Guid? EmpresaDelFiltro { get; }

    /// <summary>Si esta petición trae empresa activa en el <i>claim</i>.</summary>
    /// <remarks>
    /// Está para poder <b>preguntar</b> sin provocar la excepción. No sirve para saltarse el
    /// filtro: quien no tiene empresa y necesita consultar abre un ámbito con su motivo.
    /// </remarks>
    bool HayEmpresaActiva { get; }

    /// <summary>
    /// Abre un ámbito en el que las consultas se hacen <b>sin filtro de empresa</b>, a propósito.
    /// </summary>
    /// <remarks>
    /// El ámbito vale para el flujo asíncrono en curso y se cierra al desechar el resultado. Anidar
    /// dos está permitido y el de dentro manda; al cerrarse, se recupera el de fuera.
    /// </remarks>
    /// <param name="motivo">Por qué esta operación no tiene empresa. Va al registro.</param>
    /// <returns>El ámbito. Desecharlo lo cierra.</returns>
    IDisposable SinInquilino(MotivoSinInquilino motivo);

    /// <summary>
    /// El motivo del ámbito sin inquilino abierto ahora mismo, o <c>null</c> si no hay ninguno.
    /// </summary>
    /// <remarks>
    /// Existe para la auditoría (0.7), y es lo que hace que una fila de traza escrita sin empresa
    /// sea <b>representable</b> en vez de un hueco: la fila no lleva <see cref="Guid.Empty"/> ni
    /// una empresa inventada, lleva <c>null</c> y, al lado, por qué. «La sembró el arranque» y
    /// «alguien perdió la empresa por el camino» dejan así de parecerse.
    /// </remarks>
    MotivoSinInquilino? MotivoDelAmbito { get; }
}

/// <summary>
/// Los motivos por los que una operación puede correr <b>sin</b> filtro de empresa.
/// </summary>
/// <remarks>
/// Es una lista cerrada y no una cadena de texto libre a propósito: añadir un motivo obliga a tocar
/// este enumerado, que es un cambio que se ve en la revisión. Un <c>string</c> dejaría abrir el
/// ámbito con «temporal» y nadie se enteraría.
/// </remarks>
public enum MotivoSinInquilino
{
    /// <summary>
    /// El arranque siembra el primer usuario y su pertenencia. Todavía no hay nadie que pueda
    /// tener un <i>claim</i>, así que por definición no hay empresa activa.
    /// </summary>
    SemillaDeArranque,

    /// <summary>
    /// El acceso y el refresco: se busca al usuario por su correo, o al token por su resumen,
    /// <b>antes</b> de saber en qué empresa se entra. Filtrar aquí sería pedirle a la petición el
    /// dato que la petición viene a obtener.
    /// </summary>
    AutenticacionYSesion,

    /// <summary>
    /// Unicidad que es de la instalación entera y no de una empresa: el NIF de una empresa y el
    /// correo de un usuario. Filtrada, la comprobación diría «libre» sobre un valor ocupado y el
    /// alta acabaría estrellándose contra el índice único — un <c>500</c> donde toca un <c>409</c>.
    /// </summary>
    UnicidadGlobal,

    /// <summary>
    /// Administrar pertenencias, que es la operación que <b>por definición</b> habla de una empresa
    /// que no es la activa: dar de alta al primero de una empresa recién creada (el arranque en
    /// frío del 0.5) y mirar si esa empresa ya tiene a alguien dentro.
    /// </summary>
    AdministracionDePertenencias,
}
