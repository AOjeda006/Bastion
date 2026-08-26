using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Application.Usuarios;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Identidad.Domain.Usuarios;

namespace Bastion.Identidad.Application.Sesiones;

/// <summary>Abre una sesión.</summary>
/// <remarks>
/// No lleva permiso, y es de las poquísimas cosas que no lo llevan: es la operación con la que se
/// consiguen los permisos. Junto con la renovación, es lo único que el borde marca como anónimo.
/// </remarks>
public interface IIniciarSesion
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Correo, contraseña y, si acaso, con qué empresa empezar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<SesionAbierta>> EjecutarAsync(IniciarSesionDto peticion, CancellationToken cancelacion);
}

/// <inheritdoc cref="IIniciarSesion"/>
/// <remarks>
/// <para>
/// <b>Todas las salidas por lo mismo.</b> Correo mal escrito, cuenta que no existe, contraseña
/// incorrecta, cuenta bloqueada, cuenta rechazando intentos, empresa a la que no pertenece: los
/// seis devuelven <see cref="ErroresDeSesion.Credenciales"/>, con el mismo código y el mismo texto.
/// Cualquier bifurcación que se note desde fuera —un mensaje distinto, un código distinto o un
/// tiempo distinto— convierte el formulario de acceso en una consulta: «¿tenéis cuenta de este
/// correo?».
/// </para>
/// <para>
/// Por eso <b>la comprobación de la contraseña se hace siempre</b>, aunque ya se sepa que va a
/// fallar, contra <c>HashDeRelleno</c> cuando no hay usuario. Es la mitad del disfraz que no se ve
/// leyendo la respuesta.
/// </para>
/// </remarks>
internal sealed class IniciarSesion(
    IRepositorioDeUsuarios usuarios,
    IHasherDeContrasenas hasher,
    ConstructorDeSesion constructor,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    TimeProvider reloj) : IIniciarSesion
{
    public async Task<Resultado<SesionAbierta>> EjecutarAsync(
        IniciarSesionDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        DateTimeOffset ahora = reloj.GetUtcNow();

        // Un correo con forma imposible no llega a consultarse, pero tampoco sale antes de tiempo:
        // el usuario se queda en nulo y el camino sigue siendo el mismo, resumen de relleno
        // incluido.
        Usuario? usuario = Correo.Intentar(peticion.Correo, out Correo? correo)
            ? await usuarios.ObtenerPorCorreoAsync(correo!, cancelacion).ConfigureAwait(false)
            : null;

        ResultadoDeComprobacion comprobacion = hasher.Comprobar(
            usuario?.HashDeContrasena ?? hasher.HashDeRelleno,
            peticion.Contrasena);

        if (usuario is null || comprobacion == ResultadoDeComprobacion.Incorrecta)
        {
            // El contador solo sube cuando hay cuenta a la que subírselo. Si no la hay, no hay
            // nada que bloquear: el bloqueo por intentos protege una cuenta concreta, no el
            // formulario.
            if (usuario is not null)
            {
                usuario.RegistrarIntentoFallido(ahora);
                await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);
            }

            return Resultado.Fallo<SesionAbierta>(ErroresDeSesion.Credenciales());
        }

        // La contraseña era la buena. Comprobar el estado DESPUÉS y no antes es deliberado: así
        // una cuenta bloqueada tarda lo mismo que una activa, y no se puede averiguar cuáles
        // existen mirando cuál contesta rápido.
        if (!usuario.PuedeIniciarSesion(ahora))
        {
            return Resultado.Fallo<SesionAbierta>(ErroresDeSesion.Credenciales());
        }

        // La empresa activa: la pedida si pertenece a ella, y si no se ha pedido ninguna, la
        // primera. Que la petición pueda SUGERIR con cuál empezar no contradice R8: lo que manda
        // es que la elegida quede en el token y que a partir de ahí nadie la lea de la petición.
        Membresia? membresia = peticion.EmpresaId is Guid pedida
            ? usuario.EnEmpresa(pedida)
            : usuario.Membresias.FirstOrDefault();

        if (membresia is null)
        {
            return Resultado.Fallo<SesionAbierta>(ErroresDeSesion.Credenciales());
        }

        if (comprobacion == ResultadoDeComprobacion.CorrectaPeroConvieneRehashear)
        {
            // El único instante en el que la contraseña en claro está disponible. Aprovecharlo es
            // lo que hace que subir el coste del algoritmo llegue a las cuentas que ya existen.
            usuario.CambiarContrasena(hasher.Hashear(peticion.Contrasena));
        }

        usuario.RegistrarAccesoCorrecto(ahora);

        // Familia nueva: cada inicio de sesión empieza su propia cadena de rotaciones, para que
        // cerrar una no cierre las demás y para que detectar una reutilización solo tire abajo
        // la cadena afectada.
        SesionArmada armada = await constructor
            .ArmarAsync(usuario, membresia.EmpresaId, Guid.CreateVersion7(), cancelacion)
            .ConfigureAwait(false);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(armada.Salida);
    }
}
