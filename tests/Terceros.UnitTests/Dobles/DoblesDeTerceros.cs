using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Terceros.Application;
using Bastion.Terceros.Application.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.UnitTests.Dobles;

/// <summary>
/// El puerto de tarifas, fijado en un estado, que apunta con qué se le preguntó.
/// </summary>
/// <remarks>
/// <para>
/// <b>Apunta el identificador Y la fecha, y las dos hacen falta.</b> Sin el identificador, un caso
/// de uso que <b>no llamara</b> al puerto y guardara la tarifa tal cual pasaría el caso de «se
/// asigna» sin haber preguntado nada: verde por no hacer el trabajo. Sin la fecha, la mitad de la
/// pregunta que decide —una tarifa rige <i>en un día</i>— se quedaría sin testigo, y preguntar con
/// <c>DateOnly.MinValue</c> saldría igual de verde.
/// </para>
/// <para>
/// Es el gemelo de <c>DivisasEn</c> y <c>UnidadesEn</c> de Catálogo: mismo patrón, otro módulo,
/// otro enumerado. Y son dos clases y no una porque los dos enumerados viven en dos
/// <c>Contracts</c> que no se ven entre sí, que es justo lo que impide que el cruce mutuo del
/// ítem 1.10 sea un ciclo.
/// </para>
/// </remarks>
internal sealed class TarifasEn(EstadoDeLaTarifa estado) : IConsultaDeTarifas
{
    internal List<(Guid Tarifa, DateOnly EnLaFecha)> Preguntadas { get; } = [];

    public Task<EstadoDeLaTarifa> EstadoDeAsync(
        Guid tarifaId,
        DateOnly enLaFecha,
        CancellationToken cancelacion)
    {
        Preguntadas.Add((tarifaId, enLaFecha));

        return Task.FromResult(estado);
    }
}

/// <summary>Un almacén de terceros en memoria, con solo lo que estos casos de uso piden.</summary>
/// <remarks>
/// Las consultas que no se usan lanzan en vez de devolver algo inofensivo: si un caso de uso
/// empezara a llamarlas, este doble tiene que decirlo en vez de fingir una respuesta que nadie ha
/// decidido.
/// </remarks>
internal sealed class TercerosEnMemoria : IRepositorioDeTerceros
{
    internal List<Tercero> Guardados { get; } = [];

    public IReadOnlySet<string> CamposOrdenables { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "razonSocial" };

    internal TercerosEnMemoria Con(Tercero tercero)
    {
        Guardados.Add(tercero);

        return this;
    }

    public Task<Tercero?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        Task.FromResult(Guardados.Find(uno => uno.Id == id));

    public Task<Tercero?> ObtenerConLoQueCuelgaAsync(Guid id, CancellationToken cancelacion) =>
        ObtenerAsync(id, cancelacion);

    public Task<bool> ExisteLaIdentificacionAsync(
        Guid empresaId,
        string pais,
        string numero,
        CancellationToken cancelacion) =>
        throw new NotSupportedException("El duplicado se prueba donde hay base de datos.");

    public Task<IReadOnlySet<(string Pais, string Numero)>> IdentificacionesOcupadasAsync(
        Guid empresaId,
        IReadOnlyCollection<(string Pais, string Numero)> identificaciones,
        CancellationToken cancelacion) =>
        throw new NotSupportedException("La importación se prueba donde hay base de datos.");

    public Task<PaginaDe<Tercero>> ListarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        throw new NotSupportedException("El listado se prueba contra PostgreSQL, no aquí.");

    public Task<TramoDe<Tercero>> BuscarAsync(
        CriterioDeTerceros criterio,
        Guid? desde,
        int tamanio,
        CancellationToken cancelacion) =>
        throw new NotSupportedException("La búsqueda se prueba contra PostgreSQL, no aquí.");

    public void Agregar(Tercero tercero) => Guardados.Add(tercero);
}

/// <summary>Una unidad de trabajo que cuenta las confirmaciones y no guarda nada.</summary>
internal sealed class ConfirmacionesDeTerceros : IUnidadTrabajoDeTerceros
{
    internal int Veces { get; private set; }

    public Task<int> ConfirmarAsync(CancellationToken cancelacion)
    {
        Veces++;

        return Task.FromResult(1);
    }
}

/// <summary>Versiones que no exigen nada: la concurrencia se prueba contra PostgreSQL.</summary>
internal sealed class VersionesDeTercerosQueDanIgual : IVersionesDeTerceros
{
    public VersionDeRecurso De(object entidad) => new(0);

    public void Exigir(object entidad, VersionDeRecurso version)
    {
        // Sin efecto a propósito: lo que esta interfaz protege —dos escrituras simultáneas sobre
        // la misma fila— no es una propiedad de un objeto en memoria.
    }
}

/// <summary>Un reloj parado en un instante conocido.</summary>
internal sealed class RelojDeTercerosParado(DateTimeOffset momento) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => momento;
}
