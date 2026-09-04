using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Empresas;

namespace Bastion.Organizacion.Application.Empresas;

/// <summary>Acceso a las empresas guardadas.</summary>
/// <remarks>
/// El puerto lo declara la capa que lo CONSUME y lo implementa Infrastructure
/// (`principios/clean-architecture.md`). Ninguno de sus métodos confirma nada: eso lo decide el
/// caso de uso a través de <c>IUnidadTrabajo</c>.
/// </remarks>
public interface IRepositorioDeEmpresas : IOrdenaPor
{
    /// <summary>La empresa con ese identificador, o nulo si no hay ninguna.</summary>
    Task<Empresa?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Indica si ya hay una empresa con ese NIF.</summary>
    /// <remarks>
    /// Pide el <see cref="Nif"/> entero y no su cadena a propósito. En la base, el NIF es un
    /// valor convertido: EF Core sabe traducir una comparación contra el objeto completo, pero
    /// <b>no</b> sabe entrar en él —<c>empresa.Nif.Valor == cadena</c> revienta en ejecución con
    /// «no se pudo traducir la expresión»—. Con la cadena en la firma, ese error estaba a un
    /// descuido de distancia; con el tipo, no se puede escribir.
    /// </remarks>
    /// <param name="nif">NIF ya validado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ExisteConNifAsync(Nif nif, CancellationToken cancelacion);

    /// <summary>Si la empresa existe Y está activa.</summary>
    /// <remarks>
    /// <para>
    /// Es lo que se pregunta de la empresa del <i>claim</i>, y no basta con que exista: una empresa
    /// bloqueada por el art. 32 no puede recibir altas, o se le seguirían colgando almacenes y
    /// ejercicios a una ficha que se dio de baja.
    /// </para>
    /// <para>
    /// <b>Desde el 0.10 la mitad «y está activa» la pone el filtro de repositorio</b> y no la
    /// consulta. Aquí había además un <c>ExisteAsync</c> que preguntaba solo por la existencia;
    /// con el filtro puesto los dos devolvían exactamente lo mismo, y el que sobraba era el que
    /// se leía como «existe aunque esté bloqueada», que ya no es verdad. Se ha ido.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> EstaActivaAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Una página de empresas, con el total.</summary>
    Task<PaginaDe<Empresa>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Un tramo de empresas que cumplen el criterio, y por dónde seguir.</summary>
    /// <remarks>
    /// <para>
    /// <b>No devuelve total</b>, y no es un olvido: contar un conjunto filtrado cuesta un
    /// recorrido entero en cada tramo, que es justo lo que un cursor viene a evitar. El listado
    /// ordinario sí lo lleva porque su total es el de la tabla y sale barato.
    /// </para>
    /// <para>
    /// <b>La posición entra ya leída</b>, como <see cref="Guid"/> y no como el cursor en crudo.
    /// Un cursor que no se entiende es una entrada del cliente y su desenlace es un <c>400</c>,
    /// que aquí no se sabría dar: este puerto no devuelve <c>Resultado</c> (ADR-0004). Lo lee el
    /// caso de uso, que sí puede contestarlo.
    /// </para>
    /// </remarks>
    /// <param name="criterio">Lo que se busca, ya comprobado.</param>
    /// <param name="desde">Última empresa entregada, o nulo para empezar por el principio.</param>
    /// <param name="tamanio">Cuántas se piden.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<TramoDe<Empresa>> BuscarAsync(
        CriterioDeEmpresas criterio,
        Guid? desde,
        int tamanio,
        CancellationToken cancelacion);

    /// <summary>Apunta una empresa nueva. No la graba: eso lo hace la unidad de trabajo.</summary>
    void Agregar(Empresa empresa);
}
