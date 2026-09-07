using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Terceros.Application.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Las cuentas bancarias de un tercero.</summary>
public interface IListarCuentasBancarias
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha de la que cuelgan.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<IReadOnlyList<CuentaBancariaDto>>> EjecutarAsync(
        Guid terceroId,
        CancellationToken cancelacion);
}

/// <summary>Cuelga una cuenta bancaria de un tercero.</summary>
public interface IAgregarCuentaBancaria
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha de la que cuelga.</param>
    /// <param name="version">La versión de la FICHA sobre la que se escribe.</param>
    /// <param name="peticion">Datos de la cuenta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<CuentaBancariaDto>> EjecutarAsync(
        Guid terceroId,
        VersionDeRecurso version,
        CuentaBancariaDeAltaDto peticion,
        CancellationToken cancelacion);
}

/// <summary>Hace preferente una de las cuentas de un tercero.</summary>
public interface IMarcarCuentaPreferente
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha de la que cuelga.</param>
    /// <param name="cuentaId">Cuál.</param>
    /// <param name="version">La versión de la FICHA sobre la que se escribe.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<CuentaBancariaDto>> EjecutarAsync(
        Guid terceroId,
        Guid cuentaId,
        VersionDeRecurso version,
        CancellationToken cancelacion);
}

/// <summary>Quita una cuenta bancaria de un tercero.</summary>
public interface IQuitarCuentaBancaria
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha de la que cuelga.</param>
    /// <param name="cuentaId">Cuál.</param>
    /// <param name="version">La versión de la FICHA sobre la que se escribe.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(
        Guid terceroId,
        Guid cuentaId,
        VersionDeRecurso version,
        CancellationToken cancelacion);
}

/// <summary>Los desenlaces fallidos propios de las cuentas.</summary>
internal static class ErroresDeCuenta
{
    internal static ErrorDeOperacion NoEncontrada(Guid id) => ErrorDeOperacion.NoEncontrado(
        "cuenta-bancaria-no-encontrada",
        $"Esta ficha no tiene ninguna cuenta bancaria con el identificador {id}.");

    /// <summary>
    /// La cuenta ya está en la ficha. <b>Sin el IBAN dentro</b>, igual que el identificador fiscal
    /// duplicado: la respuesta de un conflicto no es sitio para devolver un número de cuenta, y
    /// quien la ha enviado ya lo tiene.
    /// </summary>
    internal static ErrorDeOperacion Duplicada() => ErrorDeOperacion.Conflicto(
        "cuenta-bancaria-duplicada",
        "Esta ficha ya tiene esa cuenta bancaria.");
}

/// <inheritdoc cref="IListarCuentasBancarias"/>
internal sealed class ListarCuentasBancarias(IRepositorioDeTerceros terceros)
    : IListarCuentasBancarias
{
    public async Task<Resultado<IReadOnlyList<CuentaBancariaDto>>> EjecutarAsync(
        Guid terceroId,
        CancellationToken cancelacion)
    {
        Tercero? tercero = await terceros
            .ObtenerConLoQueCuelgaAsync(terceroId, cancelacion)
            .ConfigureAwait(false);

        return tercero is null
            ? Resultado.Fallo<IReadOnlyList<CuentaBancariaDto>>(
                ErroresDeTercero.NoEncontrado(terceroId))
            : Resultado.Correcto<IReadOnlyList<CuentaBancariaDto>>(
                [.. tercero.CuentasBancarias.Select(cuenta => cuenta.ADto())]);
    }
}

/// <inheritdoc cref="IAgregarCuentaBancaria"/>
internal sealed class AgregarCuentaBancaria(
    IRepositorioDeTerceros terceros,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    IVersionesDeTerceros versiones,
    TimeProvider reloj) : IAgregarCuentaBancaria
{
    public async Task<Resultado<CuentaBancariaDto>> EjecutarAsync(
        Guid terceroId,
        VersionDeRecurso version,
        CuentaBancariaDeAltaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Tercero? tercero = await terceros
            .ObtenerConLoQueCuelgaAsync(terceroId, cancelacion)
            .ConfigureAwait(false);

        if (tercero is null)
        {
            return Resultado.Fallo<CuentaBancariaDto>(ErroresDeTercero.NoEncontrado(terceroId));
        }

        versiones.Exigir(tercero, version);

        var errores = new ErroresPorCampo();

        // EL IBAN SE LEE POR LA PUERTA QUE NO LANZA, por el mismo motivo que el NIF: un IBAN mal
        // tecleado es un error de formulario, no una excepción del sistema, y quien lo escribió
        // merece que se le diga en qué campo. Lo que NO se le dice es CUÁL de las tres condiciones
        // falló —país, longitud o control—: con esa pista, el formulario se convierte en el
        // oráculo con el que se completa un número de cuenta a medio conocer.
        if (!Iban.Intentar(peticion.Iban, out Iban? iban))
        {
            errores.Agregar(
                "iban",
                "No es un IBAN válido: revise el país, la longitud y los dígitos de control.");
        }

        string? bic = BicValido(peticion.Bic, errores);

        if (errores.Hay || iban is null)
        {
            return Resultado.Fallo<CuentaBancariaDto>(errores.AError());
        }

        CuentaBancaria cuenta;

        try
        {
            cuenta = tercero.AgregarCuentaBancaria(
                iban, bic, peticion.Alias, peticion.EsPreferente, reloj.GetUtcNow());
        }
        catch (InvalidOperationException)
        {
            // El duplicado es un CONFLICTO, no un error de campo: el dato está bien escrito, lo
            // que pasa es que ya está.
            return Resultado.Fallo<CuentaBancariaDto>(ErroresDeCuenta.Duplicada());
        }

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(cuenta.ADto());
    }

    // La FORMA del BIC, y solo la forma: ocho u once alfanuméricos. Que ese BIC exista y sea el de
    // esa cuenta no lo puede saber este módulo sin preguntárselo al registro de SWIFT, y
    // prometerlo sin preguntar sería peor que no comprobar nada.
    private static string? BicValido(string? bic, ErroresPorCampo errores)
    {
        if (string.IsNullOrWhiteSpace(bic))
        {
            return null;
        }

        string limpio = bic.Trim().ToUpperInvariant();

        if (limpio.Length is not (CuentaBancaria.LongitudMinimaDeBic
                or CuentaBancaria.LongitudMaximaDeBic)
            || !limpio.All(char.IsAsciiLetterOrDigit))
        {
            errores.Agregar("bic", "Un BIC son 8 u 11 letras o dígitos.");

            return null;
        }

        return limpio;
    }
}

/// <inheritdoc cref="IMarcarCuentaPreferente"/>
internal sealed class MarcarCuentaPreferente(
    IRepositorioDeTerceros terceros,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    IVersionesDeTerceros versiones) : IMarcarCuentaPreferente
{
    public async Task<Resultado<CuentaBancariaDto>> EjecutarAsync(
        Guid terceroId,
        Guid cuentaId,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        Tercero? tercero = await terceros
            .ObtenerConLoQueCuelgaAsync(terceroId, cancelacion)
            .ConfigureAwait(false);

        if (tercero is null)
        {
            return Resultado.Fallo<CuentaBancariaDto>(ErroresDeTercero.NoEncontrado(terceroId));
        }

        versiones.Exigir(tercero, version);

        // Se comprueba antes de llamar al agregado, y no se traduce su excepción: una cuenta que
        // no es de esta ficha es un 404 del recurso que se ha nombrado en la URL, no un 500.
        if (!tercero.CuentasBancarias.Any(cuenta => cuenta.Id == cuentaId))
        {
            return Resultado.Fallo<CuentaBancariaDto>(ErroresDeCuenta.NoEncontrada(cuentaId));
        }

        tercero.MarcarCuentaPreferente(cuentaId);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(
            tercero.CuentasBancarias.First(cuenta => cuenta.Id == cuentaId).ADto());
    }
}

/// <inheritdoc cref="IQuitarCuentaBancaria"/>
internal sealed class QuitarCuentaBancaria(
    IRepositorioDeTerceros terceros,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    IVersionesDeTerceros versiones) : IQuitarCuentaBancaria
{
    public async Task<Resultado> EjecutarAsync(
        Guid terceroId,
        Guid cuentaId,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        Tercero? tercero = await terceros
            .ObtenerConLoQueCuelgaAsync(terceroId, cancelacion)
            .ConfigureAwait(false);

        if (tercero is null)
        {
            return Resultado.Fallo(ErroresDeTercero.NoEncontrado(terceroId));
        }

        versiones.Exigir(tercero, version);

        // Sin 404 si la cuenta no está: quitar lo que ya no está deja el mismo estado, y esa es la
        // propiedad que hace que un reintento de un cliente que perdió la respuesta no falle.
        tercero.QuitarCuentaBancaria(cuentaId);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
