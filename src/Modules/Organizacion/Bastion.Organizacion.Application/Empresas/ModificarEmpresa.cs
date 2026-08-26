using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Domain.Empresas;

namespace Bastion.Organizacion.Application.Empresas;

/// <summary>Cambia los datos de una empresa.</summary>
public interface IModificarEmpresa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<EmpresaDto>> EjecutarAsync(
        Guid id,
        ModificarEmpresaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarEmpresa"/>
internal sealed class ModificarEmpresa(IRepositorioDeEmpresas empresas, IUnidadTrabajoDeOrganizacion unidadTrabajo)
    : IModificarEmpresa
{
    public async Task<Resultado<EmpresaDto>> EjecutarAsync(
        Guid id,
        ModificarEmpresaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Empresa? empresa = await empresas.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (empresa is null)
        {
            return Resultado.Fallo<EmpresaDto>(ErroresDeEmpresa.NoEncontrada(id));
        }

        // El dominio LANZA si se modifica una empresa bloqueada, y hace bien: dentro, eso es una
        // invariante rota. Pero desde fuera es un desenlace de negocio perfectamente esperable
        // —el usuario pidió modificar algo que está bloqueado—, y tiene que salir como 409 y no
        // como 500. Por eso se comprueba aquí antes de llamar (ADR-0004).
        if (empresa.Estado == EstadoDeEmpresa.Bloqueada)
        {
            return Resultado.Fallo<EmpresaDto>(ErroresDeEmpresa.Bloqueada(id));
        }

        var errores = new ErroresPorCampo();

        if (!Divisas.EsConocida(peticion.DivisaBase))
        {
            errores.Agregar(
                "divisaBase",
                "No se conoce con cuántos decimales se redondea esa divisa, y sin eso no se puede " +
                "calcular una cuota.");
        }

        if (!Enumerados.Intentar(peticion.RegimenDeIva, out RegimenDeIva regimen))
        {
            errores.Agregar(
                "regimenDeIva",
                $"No es un régimen de IVA conocido. Admitidos: {Enumerados.Admitidos<RegimenDeIva>()}.");
        }

        if (errores.Hay)
        {
            return Resultado.Fallo<EmpresaDto>(errores.AError());
        }

        empresa.Modificar(
            peticion.RazonSocial,
            peticion.DomicilioFiscal.ADireccion(),
            peticion.DivisaBase,
            regimen);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(empresa.ADto());
    }
}
