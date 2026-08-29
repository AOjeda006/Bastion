using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>
/// Un evento declarado: su nombre en el cable y el tipo de C# que lo representa.
/// </summary>
/// <param name="Nombre">Nombre estable, con la forma <c>modulo.hecho-ocurrido</c>.</param>
/// <param name="Tipo">Tipo concreto del evento.</param>
public sealed record DeclaracionDeEvento(string Nombre, Type Tipo);

/// <summary>
/// Qué nombre lleva cada evento en la cola, y qué tipo hay detrás de cada nombre.
/// </summary>
/// <remarks>
/// <para>
/// <b>El nombre se declara, no se deduce.</b> Usar el nombre del tipo saldría gratis hoy y se
/// cobraría el día que alguien renombre la clase: las filas ya escritas quedarían con un nombre que
/// ya no resuelve, el publicador no sabría deserializarlas y el fallo aparecería en el despliegue,
/// sobre datos de producción, con la cola parada. Una cadena escrita a mano se renombra sin tocar
/// la cola, y renombrarla <b>de verdad</b> —cambiar el contrato— se ve en la revisión.
/// </para>
/// <para>
/// <b>Cada módulo declara los suyos</b> en su <c>Modulo…</c>, junto al resto de su cableado. Los
/// bloques comunes no llevan una tabla con los eventos de los dieciséis módulos: eso obligaría a
/// tocar código común para publicar un evento, que es justo la frontera del §4.
/// </para>
/// <para>
/// <b>Falla cerrado en las dos direcciones.</b> Emitir un evento sin declarar lanza en el
/// <c>SaveChanges</c> del caso de uso —o sea, en el primer test que lo ejercite—, y leer de la cola
/// un nombre desconocido no revienta el publicador: aparca esa fila y sigue con las demás.
/// </para>
/// </remarks>
public sealed class CatalogoDeEventos
{
    private readonly Dictionary<string, Type> _porNombre;
    private readonly Dictionary<Type, string> _porTipo;

    /// <summary>Arma el catálogo con lo que haya declarado cada módulo.</summary>
    /// <param name="declaraciones">Los eventos declarados en el <i>composition root</i>.</param>
    /// <exception cref="InvalidOperationException">
    /// Si dos eventos comparten nombre, o un mismo tipo se declara con dos nombres.
    /// </exception>
    public CatalogoDeEventos(IEnumerable<DeclaracionDeEvento> declaraciones)
    {
        ArgumentNullException.ThrowIfNull(declaraciones);

        _porNombre = [];
        _porTipo = [];

        foreach (DeclaracionDeEvento declaracion in declaraciones)
        {
            // Dos eventos con el mismo nombre serían dos hechos distintos indistinguibles en la
            // cola, y el segundo se deserializaría como el primero. Se descubre al arrancar, que es
            // el único momento en el que todavía no hay filas escritas con el nombre ambiguo.
            if (!_porNombre.TryAdd(declaracion.Nombre, declaracion.Tipo))
            {
                throw new InvalidOperationException(
                    $"El nombre de evento «{declaracion.Nombre}» está declarado dos veces: " +
                    $"{_porNombre[declaracion.Nombre].Name} y {declaracion.Tipo.Name}.");
            }

            if (!_porTipo.TryAdd(declaracion.Tipo, declaracion.Nombre))
            {
                throw new InvalidOperationException(
                    $"El evento {declaracion.Tipo.Name} está declarado con dos nombres: " +
                    $"«{_porTipo[declaracion.Tipo]}» y «{declaracion.Nombre}».");
            }
        }
    }

    /// <summary>Los nombres declarados, para los barridos.</summary>
    public IReadOnlyCollection<Type> Declarados => _porTipo.Keys;

    /// <summary>Cómo se llama en la cola un evento de este tipo.</summary>
    /// <param name="tipo">Tipo concreto del evento.</param>
    /// <returns>Su nombre en el cable.</returns>
    /// <exception cref="InvalidOperationException">Si nadie lo ha declarado.</exception>
    public string NombreDe(Type tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return _porTipo.TryGetValue(tipo, out string? nombre)
            ? nombre
            : throw new InvalidOperationException(
                $"El evento {tipo.Name} no está declarado. Se declara en el `Modulo…` del módulo " +
                "que lo emite, con `DeclararEvento<T>(\"modulo.hecho-ocurrido\")`. Sin nombre no " +
                "se puede escribir en la bandeja, y con el nombre del tipo se rompería al " +
                "renombrar la clase.");
    }

    /// <summary>Qué tipo hay detrás de un nombre leído de la cola.</summary>
    /// <param name="nombre">Nombre en el cable.</param>
    /// <returns>El tipo, o <c>null</c> si nadie lo declara ya.</returns>
    public Type? TipoDe(string nombre) => _porNombre.GetValueOrDefault(nombre);

    /// <summary>Si este tipo de evento está declarado.</summary>
    /// <param name="tipo">Tipo concreto del evento.</param>
    /// <returns><c>true</c> si lo está.</returns>
    public bool Conoce(Type tipo) => _porTipo.ContainsKey(tipo);

    /// <summary>Todos los tipos que son eventos de integración de un ensamblado.</summary>
    /// <remarks>
    /// Lo usa el barrido que exige que ninguno se quede sin declarar. Vive aquí y no en el test
    /// para que la definición de «esto es un evento» esté en un solo sitio.
    /// </remarks>
    /// <param name="ensamblados">Dónde buscar.</param>
    /// <returns>Los tipos concretos que heredan de <see cref="EventoDeIntegracion"/>.</returns>
    public static IEnumerable<Type> EventosDe(IEnumerable<System.Reflection.Assembly> ensamblados)
    {
        ArgumentNullException.ThrowIfNull(ensamblados);

        return ensamblados
            .SelectMany(ensamblado => ensamblado.GetTypes())
            .Where(tipo => !tipo.IsAbstract && typeof(EventoDeIntegracion).IsAssignableFrom(tipo));
    }
}
