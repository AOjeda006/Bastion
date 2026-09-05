using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Direcciones;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Terceros.Application.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Da de alta un tercero.</summary>
public interface ICrearTercero
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos del tercero que se quiere dar de alta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TerceroDto>> EjecutarAsync(CrearTerceroDto peticion, CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearTercero"/>
internal sealed class CrearTercero(
    IUsuarioActual usuarioActual,
    IRepositorioDeTerceros terceros,
    IConsultaDeEmpresas empresas,
    IAccesoALoBloqueado bloqueados,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    TimeProvider reloj) : ICrearTercero
{
    public async Task<Resultado<TerceroDto>> EjecutarAsync(
        CrearTerceroDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // La empresa sale del CLAIM y no de la petición (R8). El caso de uso no puede recibirla
        // por ningún otro camino: `CrearTerceroDto` no tiene el campo.
        Guid empresaId = usuarioActual.EmpresaId;

        if (!await empresas.EstaActivaAsync(empresaId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<TerceroDto>(ErroresDeInquilinato.EmpresaActivaNoOperativa());
        }

        var errores = new ErroresPorCampo();

        IdentificacionFiscal? identificacion = Identificaciones.Leer(
            peticion.Identificacion.Pais, peticion.Identificacion.Numero, "identificacion.", errores);

        // La invariante es del dominio y allí LANZA. Aquí se adelanta porque el usuario no ha
        // hecho nada absurdo: ha dejado sin marcar dos casillas del formulario, y merece que se le
        // diga cuáles.
        if (!peticion.EsCliente && !peticion.EsProveedor)
        {
            errores.Agregar(
                "esCliente",
                "Marque al menos uno de los dos: a un tercero se le vende, se le compra, o las " +
                "dos cosas.");
        }

        if (errores.Hay || identificacion is null)
        {
            return Resultado.Fallo<TerceroDto>(errores.AError());
        }

        // AQUÍ ESTÁ LA PROPIEDAD DE ESTE ÍTEM, y conviene leerla entera.
        //
        // La unicidad de (empresa, identificador) ABARCA TAMBIÉN LO BLOQUEADO —decisión tomada,
        // no heredada del índice: el índice único no lleva `WHERE activo`—, así que esta pregunta
        // tiene que poder ver las fichas bloqueadas o contestaría que no a un identificador que sí
        // está ocupado, y el alta chocaría después contra la restricción como un 500.
        //
        // Se abre el ámbito SOLO alrededor de la pregunta, y no de lo que viene después: si
        // llegara hasta el `ConfirmarAsync`, la grabación correría con el filtro de R16 apagado,
        // que es mucho más de lo que aquí hace falta.
        //
        // Y lo que se trae de dentro es un BOOLEANO. No es que este caso de uso decida no mirar si
        // el que estorba estaba activo o bloqueado: es que no lo tiene. El puerto no lo entrega
        // (`IRepositorioDeTerceros.ExisteLaIdentificacionAsync`), así que la respuesta no puede
        // distinguirlos ni por descuido ni por buena intención. Cuál de los dos era queda escrito
        // en el registro, desde la implementación, que es quien lo sabe.
        //
        // El tercer camino por el que se filtraría es el TIEMPO, y por eso los dos casos hacen
        // literalmente la misma consulta: un índice, una fila. No hay un camino que además cargue
        // el bloqueo, ni uno que se ahorre la lectura — no porque se hayan igualado midiendo, sino
        // porque solo hay uno.
        bool ocupada;

        using (bloqueados.ViendoLoBloqueado(MotivoParaVerLoBloqueado.ComprobacionDeUnicidadDeIdentificador))
        {
            ocupada = await terceros
                .ExisteLaIdentificacionAsync(
                    empresaId, identificacion.Pais, identificacion.Numero, cancelacion)
                .ConfigureAwait(false);
        }

        if (ocupada)
        {
            return Resultado.Fallo<TerceroDto>(ErroresDeTercero.IdentificacionDuplicada());
        }

        var tercero = Tercero.Crear(
            empresaId,
            identificacion,
            peticion.RazonSocial,
            peticion.NombreComercial,
            peticion.DomicilioFiscal.ADireccion(),
            peticion.EsCliente,
            peticion.EsProveedor,
            reloj.GetUtcNow());

        terceros.Agregar(tercero);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(tercero.ADto());
    }
}
