using Bastion.BuildingBlocks.Application.Concurrencia;
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
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<EmpresaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarEmpresaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarEmpresa"/>
internal sealed class ModificarEmpresa(
    IRepositorioDeEmpresas empresas,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IModificarEmpresa
{
    public async Task<Resultado<EmpresaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarEmpresaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Empresa? empresa = await empresas.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (empresa is null)
        {
            return Resultado.Fallo<EmpresaDto>(ErroresDeEmpresa.NoEncontrada(id));
        }

        versiones.Exigir(empresa, version);

        // Aquí NO se comprueba si está bloqueada, y desde el 0.10 no se puede: la consulta de
        // arriba ya no trae lo bloqueado, así que la respuesta ordinaria a modificar una empresa
        // bloqueada es el 404 de ahí arriba.
        //
        // No es una simplificación, es lo que pide el art. 32 de la LOPDGDD: un 409 que dijera
        // «esa empresa está bloqueada» estaría revelando que el registro existe y en qué estado
        // está, que es exactamente el tratamiento —la visualización— que el bloqueo impide. Para
        // tratarla hay que desbloquearla primero, por su puerta.
        //
        // La invariante sigue dentro de la entidad (`Modificar` lanza si está bloqueada) porque
        // ahí protege a quien la modifique DESDE un ámbito abierto a propósito, que es el único
        // sitio desde el que se puede llegar a ella.

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
