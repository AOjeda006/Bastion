using System.ComponentModel.DataAnnotations;
using Bastion.BuildingBlocks.Contracts.Direcciones;
using Bastion.BuildingBlocks.Contracts.Paginacion;

namespace Bastion.Terceros.Contracts.Terceros;

/// <summary>
/// Con qué identificador fiscal se conoce a un tercero, tal como sale de la API.
/// </summary>
/// <remarks>
/// Los tres campos van juntos y no sueltos en el tercero porque son un solo hecho: este número, de
/// este país, comprobado hasta aquí. Separados, un cliente podría leer el número y olvidarse de la
/// verificación, que es justo lo que este ítem existe para impedir.
/// </remarks>
/// <param name="Pais">País emisor, en ISO 3166-1 alfa-2.</param>
/// <param name="Numero">El identificador, ya normalizado.</param>
/// <param name="Verificacion">
/// Cuánto se ha comprobado, como texto: <c>VerificadoPorAlgoritmo</c> para un NIF, un NIE o un CIF
/// cuyo carácter de control cuadra; <c>NoVerificado</c> para todo lo demás.
/// </param>
public sealed record IdentificacionFiscalDto(string Pais, string Numero, string Verificacion);

/// <summary>Un tercero, tal como sale de la API.</summary>
/// <remarks>
/// <b>No lleva estado ni fecha de bloqueo, y su ausencia es la regla</b> (R16). El filtro de
/// repositorio deja fuera lo bloqueado, así que todo lo que sale por un camino ordinario está
/// activo por construcción: un campo <c>Estado</c> solo podría decir «activo».
/// </remarks>
/// <param name="Id">Identificador del tercero.</param>
/// <param name="EmpresaId">Empresa a la que pertenece la ficha (R8).</param>
/// <param name="Identificacion">Su identificador fiscal, con país y estado de verificación.</param>
/// <param name="RazonSocial">Razón social, o nombre y apellidos.</param>
/// <param name="NombreComercial">Nombre comercial, si opera con uno distinto.</param>
/// <param name="DomicilioFiscal">Domicilio fiscal, estructurado (R17).</param>
/// <param name="EsCliente">Se le vende.</param>
/// <param name="EsProveedor">Se le compra.</param>
public sealed record TerceroDto(
    Guid Id,
    Guid EmpresaId,
    IdentificacionFiscalDto Identificacion,
    string RazonSocial,
    string? NombreComercial,
    DireccionDto DomicilioFiscal,
    bool EsCliente,
    bool EsProveedor);

/// <summary>
/// Con qué identificador se da de alta un tercero.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sin el estado de verificación, y no es una omisión.</b> Cuánto se ha comprobado un
/// identificador no es una opinión de quien rellena el formulario: es lo que el servidor sabe
/// después de mirarlo. Un campo que el cliente pudiera poner y el servidor tuviera que ignorar
/// sería un campo que miente sobre lo que hace.
/// </para>
/// <para>
/// <b>Un solo par de campos para las dos clases de identificador.</b> La alternativa era un campo
/// <c>nif</c> y otro <c>identificadorExtranjero</c>, exclusivos entre sí, con la regla de «uno y
/// solo uno» que hay que escribir, comprobar y explicar. Con el país delante, la regla es la que
/// ya dice el dominio: <c>ES</c> se valida, lo demás no se puede validar y se marca como no
/// validado.
/// </para>
/// </remarks>
public sealed record IdentificacionDeAltaDto
{
    /// <summary>País emisor, en ISO 3166-1 alfa-2. <c>ES</c> exige un NIF, NIE o CIF válido.</summary>
    [Required(ErrorMessage = "El país del identificador fiscal es obligatorio.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "El país son dos letras (ISO 3166-1 alfa-2).")]
    public string Pais { get; init; } = string.Empty;

    /// <summary>El identificador. Se normaliza igual que un NIF, así que admite puntos y guiones.</summary>
    [Required(ErrorMessage = "El identificador fiscal es obligatorio.")]
    [StringLength(20, ErrorMessage = "El identificador fiscal no puede pasar de {1} caracteres.")]
    public string Numero { get; init; } = string.Empty;
}

/// <summary>Lo que hace falta para dar de alta un tercero.</summary>
/// <remarks>
/// <b>No lleva empresa</b>, y no puede llevarla: la empresa sale del claim de la sesión y nunca
/// del cuerpo (R8). Al no estar en el contrato, no hay ni siquiera manera de intentarlo.
/// </remarks>
public sealed record CrearTerceroDto
{
    /// <summary>Con qué identificador fiscal se le conoce.</summary>
    [Required(ErrorMessage = "El identificador fiscal es obligatorio.")]
    public IdentificacionDeAltaDto Identificacion { get; init; } = new();

    /// <summary>Razón social, o nombre y apellidos si es una persona física.</summary>
    [Required(ErrorMessage = "La razón social es obligatoria.")]
    [StringLength(120, ErrorMessage = "La razón social no puede pasar de {1} caracteres.")]
    public string RazonSocial { get; init; } = string.Empty;

    /// <summary>Nombre comercial, si opera con uno distinto del fiscal.</summary>
    [StringLength(120, ErrorMessage = "El nombre comercial no puede pasar de {1} caracteres.")]
    public string? NombreComercial { get; init; }

    /// <summary>Domicilio fiscal, en los seis campos de R17.</summary>
    [Required(ErrorMessage = "El domicilio fiscal es obligatorio.")]
    public DireccionDto DomicilioFiscal { get; init; } = new();

    /// <summary>Se le vende.</summary>
    public bool EsCliente { get; init; }

    /// <summary>Se le compra.</summary>
    public bool EsProveedor { get; init; }
}

/// <summary>
/// Lo que se puede cambiar de un tercero ya dado de alta.
/// </summary>
/// <remarks>
/// Sin identificador fiscal, y no por olvido: el identificador aparece en cada factura ya emitida
/// a ese tercero. Cambiarlo no es modificar al tercero, es otro tercero. Al no estar en el
/// contrato, no hay manera de intentarlo.
/// </remarks>
public sealed record ModificarTerceroDto
{
    /// <summary>Razón social, o nombre y apellidos si es una persona física.</summary>
    [Required(ErrorMessage = "La razón social es obligatoria.")]
    [StringLength(120, ErrorMessage = "La razón social no puede pasar de {1} caracteres.")]
    public string RazonSocial { get; init; } = string.Empty;

    /// <summary>Nombre comercial, si opera con uno distinto del fiscal.</summary>
    [StringLength(120, ErrorMessage = "El nombre comercial no puede pasar de {1} caracteres.")]
    public string? NombreComercial { get; init; }

    /// <summary>Domicilio fiscal, en los seis campos de R17.</summary>
    [Required(ErrorMessage = "El domicilio fiscal es obligatorio.")]
    public DireccionDto DomicilioFiscal { get; init; } = new();

    /// <summary>Se le vende.</summary>
    public bool EsCliente { get; init; }

    /// <summary>Se le compra.</summary>
    public bool EsProveedor { get; init; }
}

/// <summary>
/// El criterio con el que se busca un tercero, y por dónde seguir. Viaja en el <b>cuerpo</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí es donde el ADR-0025 se cobra.</b> En Empresas el criterio peligroso era el NIF de una
/// sociedad; en Terceros es el NIF de un cliente, que muy a menudo es una persona física. Buscar
/// por identificador fiscal es exactamente lo que quien usa una pantalla de terceros va a querer
/// hacer todos los días, así que si viajara por la cadena de consulta quedaría escrito todos los
/// días: en el historial del navegador, en el enlace que se copia por chat, en la referencia que
/// el navegador manda al sitio siguiente y en el registro de acceso del servidor de delante, que
/// suele guardarse más tiempo y con menos cuidado que la base de datos.
/// </para>
/// <para>
/// El listado ordinario sigue siendo un <c>GET</c> porque <c>page</c>, <c>size</c>, <c>sort</c> y
/// <c>q</c> no llevan nada personal — y <c>q</c> busca por razón social y nombre comercial, que es
/// lo que se lee en una pantalla, no una llave con la que cruzar ficheros.
/// </para>
/// </remarks>
public sealed record BuscarTercerosDto
{
    /// <summary>
    /// Identificador fiscal exacto. Se normaliza igual que en el alta, así que admite puntos y
    /// guiones.
    /// </summary>
    /// <remarks>
    /// Es una coincidencia EXACTA y no un «empieza por», a propósito: un identificador parcial no
    /// es un criterio de búsqueda, es un barrido del censo de nueve en nueve caracteres.
    /// </remarks>
    [StringLength(20, ErrorMessage = "El identificador fiscal no puede pasar de {1} caracteres.")]
    public string? Numero { get; init; }

    /// <summary>
    /// País del identificador, en ISO 3166-1 alfa-2. Si no se dice, se busca en España.
    /// </summary>
    /// <remarks>
    /// Tiene valor por omisión porque la búsqueda de todos los días es la española y obligar a
    /// escribir <c>ES</c> en cada petición sería una ceremonia que nadie agradece. Lo que NO tiene
    /// es la opción de buscar «en cualquier país»: el mismo número puede identificar a dos
    /// personas distintas en dos países, y una búsqueda que los mezclara enseñaría la ficha de
    /// alguien a quien no se buscaba.
    /// </remarks>
    [StringLength(2, MinimumLength = 2, ErrorMessage = "El país son dos letras (ISO 3166-1 alfa-2).")]
    public string? Pais { get; init; }

    /// <summary>Trozo de la razón social o del nombre comercial. No distingue mayúsculas.</summary>
    [StringLength(100, ErrorMessage = "El nombre buscado no puede pasar de {1} caracteres.")]
    public string? Nombre { get; init; }

    /// <summary>Por dónde seguir, tal como lo devolvió el tramo anterior. Nulo para empezar.</summary>
    [StringLength(64, ErrorMessage = "El cursor no puede pasar de {1} caracteres.")]
    public string? Cursor { get; init; }

    /// <summary>Cuántos terceros se piden en este tramo.</summary>
    [Range(1, Paginacion.TamanioMaximo, ErrorMessage = "El tamaño va de {1} a {2}.")]
    public int Tamanio { get; init; } = Paginacion.TamanioPorDefecto;
}
