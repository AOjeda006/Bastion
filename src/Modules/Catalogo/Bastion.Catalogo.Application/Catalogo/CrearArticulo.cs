using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Comun;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Unidades;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Da de alta un artículo.</summary>
public interface ICrearArticulo
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos del artículo que se quiere dar de alta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ArticuloDto>> EjecutarAsync(CrearArticuloDto peticion, CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearArticulo"/>
/// <remarks>
/// <b>Este caso de uso es el consumidor para el que se construyó la retirada del ADR-0023.</b>
/// Hasta aquí, <c>EstadoDeMaestro</c> era una forma que dos puertos sabían devolver y nadie
/// ramificaba. Las tres casillas se traducen en <see cref="ElMaestroSeOfreceParaLoNuevo"/>, con la
/// segunda —<c>SoloResuelveLoViejo</c>— rechazando el alta <b>sin</b> tocar a los artículos que ya
/// apuntan a esa fila, que siguen resolviendo su unidad. Esa segunda mitad no se ve desde aquí
/// porque consiste precisamente en que aquí no se hace nada: quien la sostiene es
/// <see cref="ModificarArticulo"/>, que no vuelve a preguntar por lo que no ha cambiado.
/// </remarks>
internal sealed class CrearArticulo(
    IUsuarioActual usuarioActual,
    IRepositorioDeArticulos articulos,
    IRepositorioDeCategorias categorias,
    IConsultaDeEmpresas empresas,
    IConsultaDeUnidadesDeMedida unidades,
    IConsultaDeImpuestos impuestos,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    TimeProvider reloj) : ICrearArticulo
{
    public async Task<Resultado<ArticuloDto>> EjecutarAsync(
        CrearArticuloDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // La empresa sale del CLAIM y no de la petición (R8). El caso de uso no puede recibirla
        // por ningún otro camino: `CrearArticuloDto` no tiene el campo.
        Guid empresaId = usuarioActual.EmpresaId;

        if (!await empresas.EstaActivaAsync(empresaId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<ArticuloDto>(ErroresDeInquilinato.EmpresaActivaNoOperativa());
        }

        TipoDeArticulo? tipo = TiposDeArticulo.Leer(peticion.Tipo);

        if (tipo is null)
        {
            return Resultado.Fallo<ArticuloDto>(
                ErroresDeArticulo.TipoNoValido(TiposDeArticulo.Admitidos));
        }

        string codigo = Articulo.NormalizarCodigo(peticion.Codigo);

        if (await articulos.ExisteElCodigoAsync(empresaId, codigo, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<ArticuloDto>(ErroresDeArticulo.CodigoDuplicado(codigo));
        }

        // LOS DOS PUERTOS DEL ÍTEM 1.2, Y AQUÍ ES DONDE DEJAN DE SER DECORATIVOS.
        //
        // Ni la unidad ni el impuesto tienen clave ajena: viven en el esquema de Organización y
        // entre esquemas no se cruza (§5, regla 4). Sin estas dos preguntas, las dos columnas
        // aceptarían cualquier `uuid`, compilarían, migrarían y pasarían los tests — y el fallo
        // aparecería en la factura que los usa, tres fases después. Es la «cuarta vía» del
        // ADR-0024, y este es el sitio exacto donde se tapa.
        EstadoDeMaestro estadoDeLaUnidad = await unidades
            .EstadoDeAsync(peticion.UnidadBaseId, cancelacion)
            .ConfigureAwait(false);

        Resultado laUnidad = ElMaestroSeOfreceParaLoNuevo.LaUnidad(
            estadoDeLaUnidad, peticion.UnidadBaseId);

        if (!laUnidad.EsCorrecto)
        {
            return Resultado.Fallo<ArticuloDto>(laUnidad.Error!);
        }

        // El devengo con el que se pregunta por el tramo es el DÍA DEL ALTA, y hay que decir por
        // qué se puede elegir así: lo que aquí se guarda es una PROPUESTA para cuando se facture,
        // no el impuesto de ninguna operación. Un tramo que rige hoy es el que tiene sentido
        // proponer hoy; el que de verdad se aplique lo decidirá la factura con SU fecha de
        // devengo, en la fase 5, volviendo a preguntar a este mismo puerto.
        //
        // En UTC, como todo instante del sistema: la diferencia con la hora peninsular solo mueve
        // el día durante una o dos horas de madrugada, y el precio de equivocarse ahí es proponer
        // el tramo de ayer en una ficha que se puede corregir — no una cuota mal calculada.
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);

        EstadoDeMaestro estadoDelImpuesto = await impuestos
            .EstadoDeAsync(peticion.ImpuestoPorDefectoId, hoy, cancelacion)
            .ConfigureAwait(false);

        Resultado elImpuesto = ElMaestroSeOfreceParaLoNuevo.ElImpuesto(
            estadoDelImpuesto, peticion.ImpuestoPorDefectoId, hoy);

        if (!elImpuesto.EsCorrecto)
        {
            return Resultado.Fallo<ArticuloDto>(elImpuesto.Error!);
        }

        // La categoría es del PROPIO módulo, así que no hay puerto que cruzar: se comprueba
        // leyendo. Y se comprueba, aunque no haya clave ajena que lo obligue —la hay, es del mismo
        // esquema— porque el mensaje de un identificador inventado tiene que decir cuál.
        if (peticion.CategoriaId is { } categoriaId
            && await categorias.EslabonAsync(categoriaId, cancelacion).ConfigureAwait(false) is null)
        {
            return Resultado.Fallo<ArticuloDto>(ErroresDeCategoria.NoEncontrada(categoriaId));
        }

        var articulo = Articulo.Crear(
            empresaId,
            codigo,
            peticion.Descripcion,
            tipo.Value,
            peticion.UnidadBaseId,
            peticion.ImpuestoPorDefectoId,
            peticion.CategoriaId,
            reloj.GetUtcNow());

        articulos.Agregar(articulo);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(articulo.ADto());
    }
}
