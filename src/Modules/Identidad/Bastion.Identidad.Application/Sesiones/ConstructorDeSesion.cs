using Bastion.Identidad.Application.Roles;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Identidad.Domain.Sesiones;
using Bastion.Identidad.Domain.Usuarios;
using Bastion.Organizacion.Contracts.Empresas;

namespace Bastion.Identidad.Application.Sesiones;

/// <summary>
/// Arma una sesión: reúne los permisos del usuario en la empresa activa, emite el token de acceso
/// y apunta la emisión del de refresco.
/// </summary>
/// <remarks>
/// Está aquí y no repetido en el login, la renovación y el cambio de empresa porque las tres cosas
/// tienen que producir <b>exactamente</b> el mismo token. Escrito tres veces, la tercera copia
/// olvidaría un <i>claim</i> —el de empresa, seguramente— y el resultado sería una sesión que
/// funciona para casi todo: la peor clase de fallo de autorización, porque no se nota.
/// </remarks>
internal sealed class ConstructorDeSesion(
    IRepositorioDeRoles roles,
    IRepositorioDeTokensDeRefresco tokens,
    IConsultaDeEmpresas empresas,
    IEmisorDeTokens emisor,
    TimeProvider reloj)
{
    /// <summary>Arma la sesión y apunta la emisión del token de refresco.</summary>
    /// <param name="usuario">Quién.</param>
    /// <param name="empresaActivaId">Empresa activa, ya comprobada contra sus pertenencias.</param>
    /// <param name="selector">
    /// Las empresas visibles del usuario, tal como las devuelve <see cref="ParaElSelectorAsync"/>.
    /// Se pasa desde fuera, y no se calcula aquí, porque quien llama la necesita <b>antes</b> para
    /// elegir o validar la empresa activa: calcularla dos veces serían dos consultas y, peor, dos
    /// listas que podrían no coincidir.
    /// </param>
    /// <param name="familiaId">Cadena de rotaciones: una nueva en el login, la misma al renovar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    internal async Task<SesionArmada> ArmarAsync(
        Usuario usuario,
        Guid empresaActivaId,
        IReadOnlyList<EmpresaDeSesionDto> selector,
        Guid familiaId,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(selector);

        Membresia membresia = usuario.EnEmpresa(empresaActivaId)
            ?? throw new InvalidOperationException(
                "Se ha intentado armar una sesión sobre una empresa a la que el usuario no " +
                "pertenece. Quien llama tiene que haberlo comprobado antes: llegar hasta aquí " +
                "significa que la comprobación se ha caído de algún camino.");

        // La empresa activa tiene que estar entre las visibles. Sin esta línea se puede emitir un
        // token cuya empresa NO sale en el selector de su propia sesión —el frontal pintaría un
        // desplegable en el que no está lo que está seleccionado—, y sobre todo se puede seguir
        // operando dentro de una empresa suprimida al amparo del art. 32, que es R16 al revés.
        if (!selector.Any(empresa => empresa.Id == empresaActivaId))
        {
            throw new InvalidOperationException(
                "Se ha intentado armar una sesión sobre una empresa que no está entre las " +
                "visibles del usuario: o está bloqueada, o la lista no es la suya. Quien llama " +
                "tiene que haberlo comprobado antes y devolver su propio error.");
        }

        IReadOnlyList<string> permisos = await roles
            .PermisosDeAsync([.. membresia.Roles.Select(rol => rol.RolId)], cancelacion)
            .ConfigureAwait(false);

        TokenDeAcceso acceso = emisor.EmitirAcceso(usuario.Id, usuario.Nombre, empresaActivaId, permisos);
        RefrescoGenerado refresco = emisor.GenerarRefresco();

        var emision = TokenDeRefresco.Emitir(
            usuario.Id,
            familiaId,
            empresaActivaId,
            refresco.Hash,
            reloj.GetUtcNow(),
            emisor.DuracionDelRefresco);

        tokens.Agregar(emision);

        var sesion = new SesionDto(
            acceso.Valor,
            acceso.ExpiraEn,
            usuario.Id,
            usuario.Nombre,
            empresaActivaId,
            selector,
            permisos);

        return new SesionArmada(
            new SesionAbierta(sesion, refresco.Valor, emision.ExpiraEn),
            emision);
    }

    /// <summary>Las empresas del usuario con su nombre, ordenadas para el desplegable.</summary>
    /// <remarks>
    /// <para>
    /// El nombre lo pone Organización, que es la dueña, por su interfaz de <c>Contracts</c>
    /// (§4, regla 3). Identidad sabe a qué empresas pertenece alguien; no sabe cómo se llaman, y no
    /// va a averiguarlo con un <c>JOIN</c> contra un esquema ajeno.
    /// </para>
    /// <para>
    /// <b>Las pertenencias sin nombre se caen de la lista.</b> Que un identificador no vuelva
    /// significa que su empresa está bloqueada —el filtro de R16 no la trae—, y una empresa
    /// suprimida no se ofrece en un selector. No se rellena con el identificador ni con un
    /// «(sin nombre)»: eso sería contar que existe, que es exactamente lo que el bloqueo impide.
    /// </para>
    /// <para>
    /// Ordenadas por nombre, y no por el orden en que devuelva la base: un desplegable que se
    /// reordena entre dos sesiones es un desplegable en el que se pulsa mal.
    /// </para>
    /// <para>
    /// <b>Es también la lista de empresas en las que se puede abrir sesión</b>, y por eso la
    /// calculan los tres casos de uso antes de elegir la activa. Que lo visible y lo operable sean
    /// la misma lista no es una comodidad: es lo que impide que quede una sesión trabajando dentro
    /// de una empresa que ya no aparece en ninguna parte.
    /// </para>
    /// </remarks>
    /// <param name="usuario">Quién.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    internal async Task<IReadOnlyList<EmpresaDeSesionDto>> ParaElSelectorAsync(
        Usuario usuario,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        Guid[] pertenece = [.. usuario.Membresias.Select(pertenencia => pertenencia.EmpresaId)];

        IReadOnlyDictionary<Guid, string> nombres = await empresas
            .RazonesSocialesDeAsync(pertenece, cancelacion)
            .ConfigureAwait(false);

        return
        [
            .. pertenece
                .Where(nombres.ContainsKey)
                .Select(empresaId => new EmpresaDeSesionDto(empresaId, nombres[empresaId]))
                .OrderBy(empresa => empresa.RazonSocial, StringComparer.CurrentCulture)
        ];
    }
}

/// <summary>La sesión armada y la emisión que la respalda.</summary>
/// <remarks>
/// La emisión se devuelve aparte porque quien renueva la necesita para enlazar la rotación
/// (<c>presentada.Canjear(emision.Id, ahora)</c>). Sin ese enlace, la cadena se rompe y la
/// detección de reutilización se queda sin familia a la que acudir.
/// </remarks>
/// <param name="Salida">Lo que va al borde: cuerpo y cookie.</param>
/// <param name="Emision">La fila del token de refresco recién apuntada.</param>
internal sealed record SesionArmada(SesionAbierta Salida, TokenDeRefresco Emision);
