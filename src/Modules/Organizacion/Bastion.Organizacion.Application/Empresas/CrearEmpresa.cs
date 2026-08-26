using Bastion.BuildingBlocks.Application;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Domain.Empresas;

namespace Bastion.Organizacion.Application.Empresas;

/// <summary>Da de alta una empresa.</summary>
public interface ICrearEmpresa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos de la empresa que se quiere dar de alta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<EmpresaDto>> EjecutarAsync(CrearEmpresaDto peticion, CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearEmpresa"/>
/// <remarks>
/// Un tipo por operación, con su interfaz, registrado en el contenedor (§3 y §4). Sin bus en
/// memoria: quién atiende cada caso de uso lo dice el compilador, no una tabla de despacho que
/// solo falla en ejecución.
/// </remarks>
internal sealed class CrearEmpresa(IRepositorioDeEmpresas empresas, IUnidadTrabajo unidadTrabajo) : ICrearEmpresa
{
    public async Task<Resultado<EmpresaDto>> EjecutarAsync(
        CrearEmpresaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var errores = new ErroresPorCampo();

        // El borde ya ha comprobado la FORMA con sus anotaciones —obligatoriedad, longitudes—.
        // Lo que queda aquí son reglas que necesitan el dominio para contestar: si el NIF tiene
        // el carácter de control que le toca, si sabemos redondear esa divisa, y si el régimen
        // es uno de los que existen (§3, «la API valida forma; el dominio valida reglas»).
        if (!Nif.Intentar(peticion.Nif, out Nif? nif))
        {
            errores.Agregar(
                "nif",
                "No es un NIF español válido: revise el carácter de control.");
        }

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

        // Pasado el bloque de validación, el NIF está construido. Se nombra una sola vez, aquí,
        // en lugar de repetir el `!` en cada uso: un `!` esparcido es una afirmación que hay que
        // volver a comprobar en cada sitio donde aparece.
        Nif identificador = nif!;

        // El NIF identifica a la empresa ante la AEAT: dos empresas con el mismo NIF no son dos
        // empresas. La base lo impide con un índice único; comprobarlo aquí es lo que convierte
        // el choque en un 409 con explicación en lugar de en una excepción de PostgreSQL.
        if (await empresas.ExisteConNifAsync(identificador, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<EmpresaDto>(ErrorDeOperacion.Conflicto(
                "empresa-ya-registrada",
                $"Ya hay una empresa dada de alta con el NIF {identificador.Valor}."));
        }

        var empresa = Empresa.Crear(
            identificador,
            peticion.RazonSocial,
            peticion.DomicilioFiscal.ADireccion(),
            peticion.DivisaBase,
            regimen);

        empresas.Agregar(empresa);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(empresa.ADto());
    }
}
