using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;

namespace Bastion.Identidad.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Lo que Identidad tiene bloqueado: cuentas de usuario, que son <b>personas físicas</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el caso más nítido del art. 32 y era el que faltaba.</b> Desde el ítem 1.4 un usuario se
/// puede bloquear con <c>MotivoDeBloqueo.SupresionSolicitada</c> —alguien ejerce su derecho de
/// supresión y sus datos se reservan en vez de borrarse— y no asomaba por ningún listado: ni por el
/// camino ordinario, que es lo correcto, ni por el reservado, que es donde el artículo espera
/// encontrarlo. Tapar sin poder rectificar también incumple.
/// </para>
/// <para>
/// <b>El bloqueo del art. 32 NO es el rechazo temporal por intentos fallidos.</b> El usuario tiene
/// además <c>RechazadoHasta</c>, que caduca solo y que el ADR-0017 se molesta en separar: es una
/// medida de seguridad, no una reserva de datos. Aquí se filtra por <c>Bloqueo.EstaBloqueado</c> y
/// por nada más, así que una cuenta con la contraseña fallada cinco veces no aparece en el listado
/// del artículo — y no debe.
/// </para>
/// <para>
/// <b>Se acota por membresía, y NO lo hace esta consulta.</b> <c>Usuario</c> no es
/// <c>IDeInquilino</c>: pertenece a la instalación y se ata a las empresas por <c>Membresia</c>,
/// que sí lo es. Por eso el contexto le pone un filtro global propio —«o no hay empresa activa, o
/// el usuario tiene pertenencia en ella»— que el ámbito del art. 32 <b>no</b> desactiva: el ámbito
/// abre el filtro de bloqueo y ningún otro. Sin ese filtro, el listado de una empresa enseñaría los
/// usuarios bloqueados de todas las demás; con él, se ve lo bloqueado de esta empresa (R8), igual
/// que en el resto del listado.
/// </para>
/// <para>
/// <b>Y aquí NO se repite, aunque la primera versión lo repetía.</b> Escribir además
/// <c>contexto.Membresias.Any(m =&gt; m.UsuarioId == usuario.Id)</c> parecía prudencia y era dos
/// cosas malas: duplicaba un invariante —dos sitios que hay que cambiar a la vez el día que la
/// pertenencia deje de ser el puente— y, sobre todo, <b>no se traducía</b>. Sumada a la navegación
/// que el filtro global ya expande, EF Core 10 se quedaba sin traducción para las nueve
/// combinaciones de orden de este módulo, y eso es un 500 en la pantalla del art. 32. Lo cazó
/// <c>ElListadoDelArticulo32SeTraduceEnteroTests</c> antes de salir de la rama.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto del módulo.</param>
internal sealed class ConsultaDeLoBloqueadoDeIdentidad(IdentidadDbContext contexto)
    : IConsultaDeLoBloqueado
{
    /// <inheritdoc/>
    public Task<LoBloqueadoDeUnModulo> PrimerosAsync(
        CriterioDeLoBloqueado criterio, CancellationToken cancelacion) =>
        ConsultasDeLoBloqueado.ResponderAsync(LoBloqueado(contexto), criterio, cancelacion);

    /// <summary>
    /// Los usuarios bloqueados con membresía en la empresa del contexto, proyectados.
    /// </summary>
    /// <remarks>
    /// Estática y visible al ensamblado de pruebas por el mismo motivo que su hermana de
    /// Organización: el barrido que comprueba que todo orden declarado se traduce a SQL necesita
    /// una puerta por donde pedir la consulta, y una proyección no tiene <c>Set</c>.
    /// </remarks>
    /// <param name="contexto">El contexto del módulo.</param>
    internal static IQueryable<RecursoBloqueado> LoBloqueado(IdentidadDbContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        // Solo el bloqueo. El recorte por empresa lo pone el filtro global de `Usuario`, que sigue
        // puesto: el ámbito del art. 32 abre el filtro de bloqueo y ningún otro.
        return contexto.Usuarios
            .Where(usuario => usuario.Bloqueo.EstaBloqueado)
            .Select(usuario => new RecursoBloqueado
            {
                Id = usuario.Id,
                Tipo = TipoDeRecursoBloqueado.Usuario,

                // Sin código, como la empresa y como el tercero, y por dos motivos que apuntan al
                // mismo sitio.
                //
                // El primero es que NO SE TRADUCE. `Correo` se mapea con conversor de valor
                // (`ConfiguracionDeUsuario`), así que `Correo.Valor` no es una columna: el filtro
                // compartido hace `ILIKE` sobre el código y el orden lo intercala, y EF Core empuja
                // las dos cosas hasta aquí y se queda sin traducción. Serían nueve combinaciones de
                // orden convertidas en un 500 justo en la pantalla del art. 32. Por eso ningún
                // repositorio del proyecto busca por un objeto de valor -`RepositorioDeUsuarios`
                // busca por nombre y nunca por correo-, y esto no iba a ser la excepción.
                //
                // El segundo es el del ítem 1.5 con el NIF: un correo es un dato personal, y este
                // listado enseña a personas cuyos datos se han RESERVADO -alguna, por haber pedido
                // su supresión-. Para levantar un bloqueo hecho por error basta con el nombre y el
                // identificador de la fila, que es lo que el desbloqueo pide. Dos cuentas con el
                // mismo nombre se distinguen por ese identificador, no publicando la dirección.
                Codigo = (string?)null,
                Nombre = usuario.Nombre,
                BloqueadoEn = usuario.Bloqueo.Desde!.Value,
                Motivo = usuario.Bloqueo.Motivo!.Value,
            });
    }
}
