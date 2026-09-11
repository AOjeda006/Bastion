using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Comun;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Contracts.Empresas;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Abre un tramo de tarifa.</summary>
public interface ICrearTarifa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos del tramo que se quiere abrir.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TarifaDto>> EjecutarAsync(CrearTarifaDto peticion, CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearTarifa"/>
/// <remarks>
/// <para>
/// <b>Éste es el consumidor que <c>IConsultaDeDivisas</c> llevaba esperando desde el ítem 1.2.</b>
/// Aquel puerto se declaró diciendo en su propia documentación —y en <c>Inventario.PuertasPublicas</c>—
/// que quien lo iba a usar sería «la tarifa del §7.3». Hasta hoy no tenía ninguno: era una puerta
/// declarada, compilada y sin nadie al otro lado. Las tres casillas de <c>EstadoDeMaestro</c> se
/// traducen en <see cref="ElMaestroSeOfreceParaLoNuevo.LaDivisa"/>, con la misma forma que la unidad
/// y el impuesto del alta de artículo.
/// </para>
/// <para>
/// <b>La divisa puede no ser la de la empresa, y eso se decide aquí a propósito.</b> Las tres
/// salidas eran rechazarla, aceptarla dejando constancia, o convertir — y convertir no es de este
/// ítem: no hay <c>TipoCambio</c>, y una conversión sin tipos guardados sería un número inventado.
/// Entre las otras dos se elige <b>aceptar</b>. Es la misma clase de decisión que la del
/// <c>LimiteCredito</c> del ítem 1.6 y la respuesta es la contraria, con motivo: un límite de
/// crédito <b>sin divisa</b> no era legítimo —era un número sin unidad—, mientras que una tarifa de
/// exportación en dólares es un caso real y frecuente, y rechazarla obligaría a montar una
/// instalación por mercado. Lo que hace que aceptarla no sea peligroso no es una comprobación en el
/// alta: es que <c>PrecioResueltoDto</c> lleva <b>siempre</b> la divisa pegada al precio, así que
/// nadie que reciba un precio puede tomarlo por el de la empresa. La comprobación en el alta habría
/// sido la protección débil —se hace una vez, al abrir— y la que de verdad protege se hace en cada
/// lectura.
/// </para>
/// <para>
/// <b>El solape se pregunta antes, y la base sigue siendo quien lo impide.</b> Es el mismo reparto
/// que en los tramos de impuesto del 0.15: esta consulta existe para poder contestar un <c>409</c>
/// con el motivo escrito, y no para sustituir a la restricción de exclusión — que es la única que
/// cubre dos peticiones simultáneas y cualquier otro camino de escritura.
/// </para>
/// </remarks>
internal sealed class CrearTarifa(
    IUsuarioActual usuarioActual,
    IRepositorioDeTarifas tarifas,
    IConsultaDeEmpresas empresas,
    IConsultaDeDivisas divisas,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    TimeProvider reloj) : ICrearTarifa
{
    public async Task<Resultado<TarifaDto>> EjecutarAsync(
        CrearTarifaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // La empresa sale del CLAIM y no de la petición (R8). `CrearTarifaDto` no tiene el campo.
        Guid empresaId = usuarioActual.EmpresaId;

        if (!await empresas.EstaActivaAsync(empresaId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<TarifaDto>(ErroresDeInquilinato.EmpresaActivaNoOperativa());
        }

        // La vigencia se comprueba antes de preguntar nada: un tramo que acaba antes de empezar no
        // es un conflicto con lo guardado, es una petición mal escrita, y contestarlo aquí evita
        // que el dominio tenga que lanzar por algo que llega de fuera (ADR-0004).
        if (peticion.VigenteHasta is { } hasta && hasta < peticion.VigenteDesde)
        {
            return Resultado.Fallo<TarifaDto>(
                ErroresDeTarifa.VigenciaAlReves(peticion.VigenteDesde, hasta));
        }

        EstadoDeMaestro estadoDeLaDivisa = await divisas
            .EstadoDeAsync(peticion.DivisaId, cancelacion)
            .ConfigureAwait(false);

        Resultado laDivisa = ElMaestroSeOfreceParaLoNuevo.LaDivisa(
            estadoDeLaDivisa, peticion.DivisaId);

        if (!laDivisa.EsCorrecto)
        {
            return Resultado.Fallo<TarifaDto>(laDivisa.Error!);
        }

        string codigo = Tarifa.NormalizarCodigo(peticion.Codigo);

        if (await tarifas
                .HaySolapeAsync(
                    empresaId, codigo, peticion.VigenteDesde, peticion.VigenteHasta, null, cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<TarifaDto>(ErroresDeTarifa.VigenciasSolapadas(codigo));
        }

        var tarifa = Tarifa.Crear(
            empresaId,
            codigo,
            peticion.Nombre,
            peticion.DivisaId,
            peticion.VigenteDesde,
            peticion.VigenteHasta,
            reloj.GetUtcNow());

        tarifas.Agregar(tarifa);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(tarifa.ADto());
    }
}
