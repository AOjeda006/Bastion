using Bastion.BuildingBlocks.Application;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.Catalogo.Application;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Unidades;

namespace Bastion.Catalogo.UnitTests.Dobles;

/// <summary>Un usuario con una empresa dentro, que es todo lo que estos casos de uso le piden.</summary>
internal sealed class UsuarioDe(Guid empresaId) : IUsuarioActual
{
    public bool EstaAutenticado => true;

    public Guid UsuarioId { get; } = Guid.NewGuid();

    public Guid EmpresaId { get; } = empresaId;

    public bool Tiene(Permiso permiso) => true;
}

/// <summary>Una empresa que contesta lo que se le diga que conteste.</summary>
/// <remarks>
/// Las otras dos preguntas del puerto lanzan a propósito en vez de devolver algo inofensivo: si
/// un caso de uso de Catálogo empezara a usarlas, este doble tiene que decirlo en vez de fingir
/// una respuesta que nadie ha decidido.
/// </remarks>
internal sealed class EmpresasQueContestan(bool activa) : IConsultaDeEmpresas
{
    public Task<bool> EstaActivaAsync(Guid empresaId, CancellationToken cancelacion) =>
        Task.FromResult(activa);

    public Task<Guid?> PrimeraActivaAsync(CancellationToken cancelacion) =>
        throw new NotSupportedException("Catálogo no elige empresa: la recibe en el claim.");

    public Task<IReadOnlyDictionary<Guid, string>> RazonesSocialesDeAsync(
        IReadOnlyCollection<Guid> empresas,
        CancellationToken cancelacion) =>
        throw new NotSupportedException("Catálogo no publica razones sociales de nadie.");
}

/// <summary>El puerto de unidades, fijado en un estado.</summary>
/// <remarks>
/// Guarda con qué identificador se le preguntó: sin eso, un caso de uso que <b>no llamara</b> al
/// puerto y devolviera correcto pasaría los tres casos de «se ofrece» sin haber preguntado nada.
/// </remarks>
internal sealed class UnidadesEn(EstadoDeMaestro estado) : IConsultaDeUnidadesDeMedida
{
    internal List<Guid> Preguntadas { get; } = [];

    public Task<EstadoDeMaestro> EstadoDeAsync(Guid unidadId, CancellationToken cancelacion)
    {
        Preguntadas.Add(unidadId);

        return Task.FromResult(estado);
    }
}

/// <summary>El puerto de impuestos, fijado en un estado, que apunta fecha y todo.</summary>
internal sealed class ImpuestosEn(EstadoDeMaestro estado) : IConsultaDeImpuestos
{
    internal List<(Guid Impuesto, DateOnly Devengo)> Preguntados { get; } = [];

    public Task<EstadoDeMaestro> EstadoDeAsync(
        Guid impuestoId,
        DateOnly enLaFechaDeDevengo,
        CancellationToken cancelacion)
    {
        Preguntados.Add((impuestoId, enLaFechaDeDevengo));

        return Task.FromResult(estado);
    }
}

/// <summary>Un almacén de artículos en memoria.</summary>
internal sealed class ArticulosEnMemoria : IRepositorioDeArticulos
{
    internal List<Articulo> Guardados { get; } = [];

    internal HashSet<string> CodigosOcupados { get; } = new(StringComparer.Ordinal);

    public IReadOnlySet<string> CamposOrdenables { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "codigo", "descripcion" };

    public Task<Articulo?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        Task.FromResult(Guardados.Find(uno => uno.Id == id));

    public Task<bool> ExisteElCodigoAsync(Guid empresaId, string codigo, CancellationToken cancelacion) =>
        Task.FromResult(CodigosOcupados.Contains(codigo));

    public Task<PaginaDe<Articulo>> ListarAsync(
        Paginacion paginacion,
        Guid? categoriaId,
        CancellationToken cancelacion) =>
        throw new NotSupportedException("El listado se prueba contra PostgreSQL, no aquí.");

    public void Agregar(Articulo articulo) => Guardados.Add(articulo);
}

/// <summary>
/// Un árbol de categorías en memoria, con un tope de llamadas que hace ruido en vez de colgarse.
/// </summary>
/// <remarks>
/// <para>
/// <b>El tope es la pieza que permite probar la cota del recorrido.</b> Sin él, el caso que ejerce
/// un ciclo guardado contra un ascenso SIN cota no falla: se queda dando vueltas para siempre, y
/// lo que ve quien lo ejecuta es una suite colgada. Un test que solo se manifiesta agotando el
/// tiempo de la CI no es un test, es una avería.
/// </para>
/// <para>
/// Con el tope, quitar la cota de <c>ElArbolSigueSiendoUnArbol</c> hace que el caso falle de
/// inmediato y con un mensaje que dice exactamente lo que pasó. Cien vueltas es diez veces la
/// profundidad máxima: lo bastante alto como para no estorbar a ningún recorrido legítimo, y lo
/// bastante bajo como para que la respuesta sea instantánea.
/// </para>
/// </remarks>
internal sealed class CategoriasEnMemoria : IRepositorioDeCategorias
{
    /// <summary>Cuántas veces se deja preguntar por un eslabón antes de dar la voz de alarma.</summary>
    internal const int TopeDeLlamadas = 100;

    private readonly Dictionary<Guid, EslabonDeCategoria> _eslabones = [];

    internal List<Categoria> Guardadas { get; } = [];

    internal HashSet<string> CodigosOcupados { get; } = new(StringComparer.Ordinal);

    internal int Llamadas { get; private set; }

    /// <summary>Cuántas veces se ha pedido la ascendencia entera de una categoría.</summary>
    internal int LlamadasDeAscendencia { get; private set; }

    public IReadOnlySet<string> CamposOrdenables { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "codigo", "nombre" };

    /// <summary>Mete un eslabón en el árbol: quién es, de quién cuelga y con qué código.</summary>
    internal CategoriasEnMemoria Con(Guid id, Guid? padreId, string codigo)
    {
        _eslabones[id] = new EslabonDeCategoria(id, padreId, codigo);

        return this;
    }

    /// <summary>Una cadena de <paramref name="cuantos"/> eslabones, de la raíz hacia abajo.</summary>
    /// <returns>Los identificadores, del primero —la raíz— al último.</returns>
    internal IReadOnlyList<Guid> ConCadenaDe(int cuantos)
    {
        List<Guid> cadena = [];
        Guid? padre = null;

        for (int nivel = 0; nivel < cuantos; nivel++)
        {
            var id = Guid.NewGuid();
            Con(id, padre, "N" + nivel.ToString(System.Globalization.CultureInfo.InvariantCulture));
            cadena.Add(id);
            padre = id;
        }

        return cadena;
    }

    public Task<Categoria?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        Task.FromResult(Guardadas.Find(una => una.Id == id));

    public Task<EslabonDeCategoria?> EslabonAsync(Guid id, CancellationToken cancelacion)
    {
        Llamadas++;

        if (Llamadas > TopeDeLlamadas)
        {
            throw new InvalidOperationException(
                $"El recorrido de padres ha pedido {Llamadas} eslabones, más de los " +
                $"{TopeDeLlamadas} que este doble admite. No hay ningún árbol de categorías tan " +
                "hondo: lo que hay es un ascenso SIN COTA sobre datos que ya tienen un ciclo, " +
                "que en producción sería una petición que no termina nunca.");
        }

        return Task.FromResult(_eslabones.GetValueOrDefault(id));
    }

    /// <summary>La ascendencia, en UNA llamada, y contándolas.</summary>
    /// <remarks>
    /// <b>El contador es la prueba del número de viajes.</b> Una precedencia resuelta con una
    /// consulta por nivel llamaría a <see cref="EslabonAsync"/> tantas veces como hondo esté el
    /// artículo; ésta se llama <b>una vez</b>, y la misma vez con un árbol de dos niveles que con
    /// uno de once. Sin los dos contadores, esa afirmación no se puede poner roja sin Docker.
    /// </remarks>
    public Task<IReadOnlyList<Guid>> AscendenciaAsync(Guid categoriaId, CancellationToken cancelacion)
    {
        LlamadasDeAscendencia++;

        List<Guid> cadena = [];
        Guid? actual = categoriaId;

        while (actual is { } id
            && _eslabones.TryGetValue(id, out EslabonDeCategoria? eslabon)
            && !cadena.Contains(id))
        {
            cadena.Add(id);
            actual = eslabon.PadreId;
        }

        return Task.FromResult<IReadOnlyList<Guid>>(cadena);
    }

    public Task<bool> ExisteElCodigoAsync(Guid empresaId, string codigo, CancellationToken cancelacion) =>
        Task.FromResult(CodigosOcupados.Contains(codigo));

    public Task<PaginaDe<Categoria>> ListarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        throw new NotSupportedException("El listado se prueba contra PostgreSQL, no aquí.");

    public void Agregar(Categoria categoria) => Guardadas.Add(categoria);
}

/// <summary>Una unidad de trabajo que cuenta las confirmaciones y no guarda nada.</summary>
internal sealed class ConfirmacionesContadas : IUnidadTrabajoDeCatalogo
{
    internal int Veces { get; private set; }

    public Task<int> ConfirmarAsync(CancellationToken cancelacion)
    {
        Veces++;

        return Task.FromResult(1);
    }
}

/// <summary>Versiones que no exigen nada: la concurrencia se prueba contra PostgreSQL.</summary>
internal sealed class VersionesQueDanIgual : IVersionesDeCatalogo
{
    public VersionDeRecurso De(object entidad) => new(0);

    public void Exigir(object entidad, VersionDeRecurso version)
    {
        // Sin efecto a propósito: lo que esta interfaz protege —dos escrituras simultáneas sobre
        // la misma fila— no es una propiedad de un objeto en memoria, y fingirla aquí daría una
        // seguridad que solo PostgreSQL puede dar. La comprueban los tests de integración.
    }
}

/// <summary>Un reloj parado en un instante conocido.</summary>
internal sealed class RelojParado(DateTimeOffset momento) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => momento;
}

/// <summary>El puerto de divisas, fijado en un estado, que apunta por cuál se preguntó.</summary>
/// <remarks>
/// Guarda el identificador por lo mismo que el de unidades: sin eso, un alta de tarifa que
/// <b>no llamara</b> al puerto pasaría el caso de «se ofrece» sin haber preguntado nada, y el
/// consumidor que este ítem construye para <c>IConsultaDeDivisas</c> sería otra vez decorativo.
/// </remarks>
internal sealed class DivisasEn(EstadoDeMaestro estado) : IConsultaDeDivisas
{
    internal List<Guid> Preguntadas { get; } = [];

    public Task<EstadoDeMaestro> EstadoDeAsync(Guid divisaId, CancellationToken cancelacion)
    {
        Preguntadas.Add(divisaId);

        return Task.FromResult(estado);
    }
}

/// <summary>Un almacén de tramos de tarifa en memoria.</summary>
internal sealed class TarifasEnMemoria : IRepositorioDeTarifas
{
    internal List<Tarifa> Guardadas { get; } = [];

    /// <summary>Si la siguiente comprobación de solape tiene que decir que sí.</summary>
    internal bool DiceQueHaySolape { get; set; }

    public IReadOnlySet<string> CamposOrdenables { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "codigo", "nombre", "vigenteDesde" };

    public Task<Tarifa?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        Task.FromResult(Guardadas.Find(una => una.Id == id));

    public Task<Tarifa?> VigenteAsync(
        Guid empresaId,
        string codigo,
        DateOnly dia,
        CancellationToken cancelacion) =>
        Task.FromResult(Guardadas.Find(una =>
            una.EmpresaId == empresaId && una.Codigo == codigo && una.RigeEl(dia)));

    public Task<bool> ExisteElCodigoAsync(
        Guid empresaId,
        string codigo,
        CancellationToken cancelacion) =>
        Task.FromResult(Guardadas.Exists(una => una.EmpresaId == empresaId && una.Codigo == codigo));

    public Task<bool> HaySolapeAsync(
        Guid empresaId,
        string codigo,
        DateOnly desde,
        DateOnly? hasta,
        Guid? excepto,
        CancellationToken cancelacion) =>
        Task.FromResult(DiceQueHaySolape);

    public Task<PaginaDe<Tarifa>> ListarAsync(
        Paginacion paginacion,
        string? codigo,
        CancellationToken cancelacion) =>
        throw new NotSupportedException("El listado se prueba contra PostgreSQL, no aquí.");

    public void Agregar(Tarifa tarifa) => Guardadas.Add(tarifa);
}

/// <summary>Un almacén de líneas de tarifa en memoria, que cuenta los viajes.</summary>
/// <remarks>
/// <see cref="LlamadasDeCandidatas"/> es la otra mitad de la afirmación sobre el número de viajes:
/// las candidatas se piden <b>una vez</b> por resolución, con toda la ascendencia dentro, y no una
/// vez por nivel.
/// </remarks>
internal sealed class LineasDeTarifaEnMemoria : IRepositorioDeLineasDeTarifa
{
    internal List<LineaTarifa> Guardadas { get; } = [];

    internal int LlamadasDeCandidatas { get; private set; }

    public IReadOnlySet<string> CamposOrdenables { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "cantidadDesde" };

    public Task<LineaTarifa?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        Task.FromResult(Guardadas.Find(una => una.Id == id));

    public Task<IReadOnlyList<LineaCandidata>> CandidatasAsync(
        Guid tarifaId,
        Guid articuloId,
        IReadOnlyList<Guid> ascendencia,
        CancellationToken cancelacion)
    {
        LlamadasDeCandidatas++;

        Dictionary<Guid, int> nivelDe = [];

        for (int nivel = 0; nivel < ascendencia.Count; nivel++)
        {
            nivelDe.TryAdd(ascendencia[nivel], nivel);
        }

        IReadOnlyList<LineaCandidata> candidatas =
        [
            .. Guardadas
                .Where(linea => linea.TarifaId == tarifaId
                    && (linea.ArticuloId == articuloId
                        || (linea.CategoriaId is { } suya && nivelDe.ContainsKey(suya))))
                .Select(linea => new LineaCandidata(
                    linea.Id,
                    linea.ArticuloId,
                    linea.CategoriaId,
                    linea.CategoriaId is { } suya ? nivelDe[suya] : null,
                    linea.CantidadDesde,
                    linea.PrecioODescuento.Precio,
                    linea.PrecioODescuento.DescuentoPorcentaje)),
        ];

        return Task.FromResult(candidatas);
    }

    public Task<TramosDelDestino> TramosDelDestinoAsync(
        Guid tarifaId,
        Guid? articuloId,
        Guid? categoriaId,
        decimal cantidadDesde,
        CancellationToken cancelacion)
    {
        List<LineaTarifa> delDestino =
        [
            .. Guardadas.Where(linea => linea.TarifaId == tarifaId
                && linea.ArticuloId == articuloId
                && linea.CategoriaId == categoriaId),
        ];

        return Task.FromResult(new TramosDelDestino(
            delDestino.Count > 0,
            delDestino.Exists(linea => linea.CantidadDesde == cantidadDesde)));
    }

    public Task<PaginaDe<LineaTarifa>> ListarAsync(
        Paginacion paginacion,
        Guid tarifaId,
        CancellationToken cancelacion) =>
        throw new NotSupportedException("El listado se prueba contra PostgreSQL, no aquí.");

    public void Agregar(LineaTarifa linea) => Guardadas.Add(linea);
}
