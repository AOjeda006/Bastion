using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Application.Comun;
using Bastion.Identidad.Application.Sesiones;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Identidad.Domain.Usuarios;
using Bastion.Organizacion.Contracts.Empresas;

namespace Bastion.Identidad.Application.Usuarios;

/// <summary>Da de alta un usuario en la empresa activa.</summary>
/// <remarks>
/// <b>Solo por invitación.</b> No hay auto-registro: esto lo llama alguien que ya está dentro y
/// que tiene <c>identidad.usuario.crear</c>. La única cuenta que nace sin que nadie la invite es
/// la de la semilla de arranque, que solo se aplica si no hay ninguna.
/// </remarks>
public interface ICrearUsuario
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Correo, nombre y contraseña inicial.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<UsuarioDto>> EjecutarAsync(CrearUsuarioDto peticion, CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearUsuario"/>
internal sealed class CrearUsuario(
    IUsuarioActual usuarioActual,
    IRepositorioDeUsuarios usuarios,
    IConsultaDeEmpresas empresas,
    IInquilinoActual inquilino,
    IHasherDeContrasenas hasher,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    TimeProvider reloj) : ICrearUsuario
{
    public async Task<Resultado<UsuarioDto>> EjecutarAsync(
        CrearUsuarioDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var errores = new ErroresPorCampo();

        if (!Correo.Intentar(peticion.Correo, out Correo? correo))
        {
            errores.Agregar("correo", "No parece un correo electrónico.");
        }

        if (errores.Hay)
        {
            return Resultado.Fallo<UsuarioDto>(errores.AError());
        }

        Correo identificador = correo!;

        // Sin filtro, por lo mismo que el NIF: el correo identifica al usuario en TODA la
        // instalación —con él inicia sesión, antes de que exista empresa activa—, así que la
        // comprobación tiene que ver más allá de la empresa de quien invita.
        bool ocupado;

        using (inquilino.SinInquilino(MotivoSinInquilino.UnicidadGlobal))
        {
            ocupado = await usuarios.ExisteConCorreoAsync(identificador, cancelacion).ConfigureAwait(false);
        }

        if (ocupado)
        {
            return Resultado.Fallo<UsuarioDto>(ErroresDeUsuario.CorreoYaRegistrado(identificador.Valor));
        }

        // La empresa sale del CLAIM (R8). No hay ningún campo en `CrearUsuarioDto` por el que
        // pueda entrar otra, y esa ausencia es la regla: un parámetro de empresa en la petición
        // convertiría el permiso «crear usuarios en mi empresa» en «crear usuarios en cualquiera».
        Guid empresaId = usuarioActual.EmpresaId;

        if (!await empresas.EstaActivaAsync(empresaId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<UsuarioDto>(ErroresDeUsuario.EmpresaNoOperativa());
        }

        var usuario = Usuario.Crear(
            identificador,
            peticion.Nombre,
            hasher.Hashear(peticion.Contrasena),
            reloj.GetUtcNow());

        // Nace perteneciendo a la empresa desde la que se le invita, y sin ningún rol. Sin
        // pertenencia no podría ni iniciar sesión —no habría empresa que activar—, y con roles
        // por defecto se estaría repartiendo autoridad que nadie ha concedido.
        usuario.Conceder(empresaId);

        usuarios.Agregar(usuario);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(usuario.ADto());
    }
}
