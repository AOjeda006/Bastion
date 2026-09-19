using System.Globalization;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>Lo que un ajuste confirmado dejó escrito, con lo que hace falta para ir a buscarlo.</summary>
/// <param name="EmpresaId">Empresa dueña de todo lo de abajo (R8).</param>
/// <param name="AjusteId">El documento.</param>
/// <param name="AlmacenId">Almacén contra el que se ajustó.</param>
/// <param name="FechaDeOperacion">Día al que se imputó, que es su clave de partición.</param>
/// <param name="Movimientos">Las filas del libro que escribió.</param>
internal sealed record UnAjusteConfirmado(
    Guid EmpresaId,
    Guid AjusteId,
    Guid AlmacenId,
    DateOnly FechaDeOperacion,
    IReadOnlyList<MovimientoStock> Movimientos);

/// <summary>
/// La puerta al libro de movimientos para los casos de integración: escribe con el dominio y
/// pregunta a la base en crudo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las filas salen del dominio y del repositorio de verdad</b>, no de un <c>INSERT</c> escrito a
/// mano. Un <c>INSERT</c> montado aquí probaría una fila que el sistema no produce: el libro solo
/// se escribe confirmando un documento, y esa es justamente la mitad de la R13 que hay que poder
/// afirmar después.
/// </para>
/// <para>
/// <b>Los identificadores de fuera —almacén, ubicación, artículo, unidad— son inventados, y eso no
/// afloja nada.</b> Ninguna clave ajena cruza de esquema (§5, regla 4), así que la tabla no los
/// mira; quien los valida es el alta, por sus cuatro puertos, y eso se comprueba donde se decide
/// —<c>UnAlmacenBloqueadoNoAdmiteAjustesTests</c>, contra maestros de verdad dados de alta por la
/// API—. Lo que estos casos miran es el motor: particiones, claves, <c>CHECK</c> y disparadores.
/// </para>
/// <para>
/// <b>Toda sentencia que pueda hacer daño va en una transacción que se deshace.</b> La razón es
/// estrecha y vale la pena decirla: si un día el disparador de solo-añadido no estuviera, el caso
/// que lo comprueba <b>vaciaría el libro de verdad</b> —<c>TRUNCATE</c> no pregunta— y dejaría en
/// rojo a los demás casos de este carril por un motivo que no es el suyo. Con la transacción, un
/// disparador que falta se ve como un caso rojo y nada más.
/// </para>
/// </remarks>
internal static class ElLibro
{
    /// <summary>Nombre de la partición mensual en la que cae una fecha.</summary>
    /// <param name="fecha">La fecha de operación.</param>
    /// <returns>El nombre de la tabla, sin esquema.</returns>
    internal static string ParticionDe(DateOnly fecha) => string.Create(
        CultureInfo.InvariantCulture, $"movimiento_stock_{fecha.Year:D4}_{fecha.Month:D2}");

    /// <summary>Abre un ajuste, le pone líneas, lo confirma y guarda las dos cosas a la vez.</summary>
    /// <remarks>
    /// Es la transacción que dobla la R12 a sabiendas —el documento y las filas del libro en el
    /// mismo <c>COMMIT</c>—, hecha por el camino de producción: el agregado, el repositorio del
    /// módulo y su unidad de trabajo.
    /// </remarks>
    /// <param name="postgres">El contenedor con las migraciones puestas.</param>
    /// <param name="fechaDeOperacion">Día al que se imputa, y con él la partición de destino.</param>
    /// <param name="lineas">Cuántas líneas lleva el documento.</param>
    /// <returns>Lo que quedó escrito.</returns>
    internal static async Task<UnAjusteConfirmado> ConfirmarUnAjusteAsync(
        PostgresConTodosLosModulos postgres,
        DateOnly fechaDeOperacion,
        int lineas = 1)
    {
        var empresaId = Guid.CreateVersion7();
        var almacenId = Guid.CreateVersion7();
        DateTimeOffset momento = DateTimeOffset.UtcNow;

        var ajuste = Ajuste.Abrir(
            empresaId, almacenId, fechaDeOperacion, "Recuento de prueba del carril", momento);

        for (int numero = 1; numero <= lineas; numero++)
        {
            ajuste.AnadirLinea(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                numero,
                Guid.CreateVersion7(),
                1.5m,
                Importe.De(3.25m, "EUR"),
                momento);
        }

        var evento = new AjusteConfirmado(
            ajuste.Id, empresaId, almacenId, fechaDeOperacion, ajuste.Lineas.Count);

        IReadOnlyList<MovimientoStock> movimientos = ajuste.Confirmar(evento, momento);

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        RepositorioDeAjustes repositorio = new(contexto);
        repositorio.Agregar(ajuste);
        repositorio.AgregarMovimientos(movimientos);

        await new UnidadDeTrabajoDeInventario(contexto).ConfirmarAsync(CancellationToken.None);

        return new UnAjusteConfirmado(
            empresaId, ajuste.Id, almacenId, fechaDeOperacion, movimientos);
    }

    /// <summary>Un escalar de la base, sin EF Core por medio.</summary>
    /// <typeparam name="T">Qué se espera leer.</typeparam>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="consulta">La consulta.</param>
    /// <returns>El valor de la primera columna de la primera fila.</returns>
    internal static async Task<T> EscalarAsync<T>(
        PostgresConTodosLosModulos postgres, string consulta)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(consulta, conexion);

        object? valor = await orden.ExecuteScalarAsync();

        return (T)valor!;
    }

    /// <summary>Una columna de texto, entera.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="consulta">La consulta.</param>
    /// <returns>Las filas leídas, en el orden en que llegan.</returns>
    internal static async Task<IReadOnlyList<string>> TextosAsync(
        PostgresConTodosLosModulos postgres, string consulta)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(consulta, conexion);
        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        List<string> filas = [];

        while (await lector.ReadAsync())
        {
            filas.Add(lector.GetString(0));
        }

        return filas;
    }

    /// <summary>
    /// Ejecuta una sentencia <b>dentro de una transacción que se deshace</b> y exige que el motor
    /// la rechace.
    /// </summary>
    /// <remarks>
    /// La transacción no es prudencia genérica: es lo que convierte un disparador ausente en un
    /// caso rojo en vez de en un libro vaciado. Y se devuelve la excepción entera para que quien
    /// llama afirme <b>de qué</b> rechazo se trata: un <c>UPDATE</c> que fallara por una columna
    /// que no existe también lanzaría, y sería un verde que no ha comprobado la regla.
    /// </remarks>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="sentencia">Lo que se intenta.</param>
    /// <returns>El error que devolvió PostgreSQL.</returns>
    internal static async Task<PostgresException> ElMotorRechazaAsync(
        PostgresConTodosLosModulos postgres, string sentencia)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlTransaction transaccion = await conexion.BeginTransactionAsync();
        await using NpgsqlCommand orden = new(sentencia, conexion, transaccion);

        PostgresException fallo = await Should.ThrowAsync<PostgresException>(
            () => orden.ExecuteNonQueryAsync());

        await transaccion.RollbackAsync();

        return fallo;
    }

    /// <summary>El código SQLSTATE con el que el libro dice «aquí solo se añade».</summary>
    /// <remarks>
    /// Es el que la función del motor pone con <c>USING ERRCODE = 'restrict_violation'</c>. Se
    /// afirma el código y además la operación que aparece en el mensaje: sin lo segundo, un
    /// rechazo cualquiera con el mismo código daría el caso por bueno.
    /// </remarks>
    internal const string SoloSeAnade = "23001";
}
