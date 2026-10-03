using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.UnitTests.Valoraciones;

namespace Bastion.Inventario.UnitTests.Transferencias;

/// <summary>
/// Un libro en memoria que hace lo que hacen los casos de uso con una transferencia: valora cada pata
/// contra lo que el libro tiene, y guarda las filas y el tránsito que devuelve el documento.
/// </summary>
/// <remarks>
/// <b>No sustituye a la base</b>: el cerrojo, el orden de las sentencias y las restricciones los ve
/// el carril de integración. Aquí se ve la aritmética del documento, sin encender nada.
/// </remarks>
internal sealed class ElLibroDeLaPrueba
{
    internal const string Divisa = "EUR";

    internal static readonly DateTimeOffset Momento = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    internal static readonly Guid Empresa = Guid.Parse("0f6a1c1e-0000-4000-8000-000000000001");

    internal static readonly Guid Serie = Guid.Parse("0f6a1c1e-0000-4000-8000-000000000002");

    internal static readonly Guid Origen = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000a1");

    internal static readonly Guid Destino = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000b1");

    internal static readonly Guid UbicacionDelOrigen = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000a2");

    internal static readonly Guid UbicacionDelDestino = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000b2");

    internal static readonly Guid Articulo = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000c1");

    internal static readonly Guid Unidad = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000d1");

    private readonly List<MovimientoStock> _filas = [];

    private readonly List<MovimientoEnTransito> _transito = [];

    private long _numero = 100;

    /// <summary>Las filas del libro, en el orden en que se escribieron.</summary>
    internal IReadOnlyList<MovimientoStock> Filas => _filas;

    /// <summary>Lo que entró en tránsito y salió de él, en el orden en que se escribió.</summary>
    internal IReadOnlyList<MovimientoEnTransito> Transito => _transito;

    /// <summary>Una transferencia en borrador del origen al destino, sin líneas.</summary>
    /// <param name="fechaDeEnvio">El día en que sale.</param>
    /// <returns>La transferencia.</returns>
    internal static Transferencia UnaTransferencia(DateOnly fechaDeEnvio) =>
        Transferencia.Abrir(Empresa, Serie, Origen, Destino, fechaDeEnvio, Divisa, Momento);

    /// <summary>Una transferencia en borrador con una línea, del hueco del origen al del destino.</summary>
    /// <param name="cantidad">Cuánto, en la unidad introducida.</param>
    /// <param name="fechaDeEnvio">El día en que sale.</param>
    /// <param name="factor">Cuántas unidades base hay en una introducida.</param>
    /// <returns>La transferencia.</returns>
    internal static Transferencia UnaTransferenciaDe(decimal cantidad, DateOnly fechaDeEnvio, decimal factor = 1m)
    {
        Transferencia transferencia = UnaTransferencia(fechaDeEnvio);
        transferencia.AnadirLinea(
            UbicacionDelOrigen, UbicacionDelDestino, Articulo, cantidad, Unidad, factor, Momento);

        return transferencia;
    }

    /// <summary>Mueve existencias con un ajuste confirmado: una entrada a un coste, o una salida.</summary>
    /// <param name="almacen">Dónde.</param>
    /// <param name="ubicacion">En qué hueco.</param>
    /// <param name="cantidad">Cuánto, en unidad base: positiva entra y negativa sale.</param>
    /// <param name="coste">A cuánto la unidad, en una entrada; <c>null</c> en una salida.</param>
    /// <param name="fecha">Qué día.</param>
    internal void Ajustar(Guid almacen, Guid ubicacion, decimal cantidad, decimal? coste, DateOnly fecha)
    {
        var ajuste = Ajuste.Abrir(Empresa, Serie, almacen, fecha, "Recuento de la prueba", Divisa, Momento);
        ajuste.AnadirLinea(ubicacion, Articulo, cantidad, Unidad, 1m, coste, Momento);

        _filas.AddRange(ajuste.Confirmar(
            ++_numero,
            new AjusteConfirmado(ajuste.Id, Empresa, almacen, fecha, 1),
            LaValoracion.Tras(_filas, ajuste),
            LotesYSeriesResueltos.Ninguno,
            Momento));
    }

    /// <summary>Envía la transferencia contra lo que tiene el libro.</summary>
    /// <param name="transferencia">En borrador.</param>
    /// <returns>Lo que movió.</returns>
    internal LoQueMueveLaTransferencia Enviar(Transferencia transferencia)
    {
        IReadOnlyList<LineaValorada> valoradas = LaValoracion.DeLasLineas(
            _filas, transferencia.LineasAValorarEnElOrigen(), Divisa, transferencia.FechaDeEnvio);

        return Anotar(transferencia.Enviar(
            ++_numero, ElEnvioDe(transferencia), valoradas, LotesYSeriesResueltos.Ninguno, Momento));
    }

    /// <summary>Recibe la transferencia entera contra lo que tiene el libro.</summary>
    /// <param name="transferencia">Enviada.</param>
    /// <param name="fecha">El día en que llega.</param>
    /// <returns>Lo que movió.</returns>
    internal LoQueMueveLaTransferencia Recibir(Transferencia transferencia, DateOnly fecha)
    {
        IReadOnlyList<LineaValorada> valoradas = LaValoracion.DeLasLineas(
            _filas, transferencia.LineasAValorarEnElDestino(), Divisa, fecha);

        return Anotar(transferencia.Recibir(
            fecha, LaRecepcionDe(transferencia, fecha), valoradas, LotesYSeriesResueltos.Ninguno, Momento));
    }

    /// <summary>Anula la transferencia con su inverso, como el caso de uso: pata a pata.</summary>
    /// <param name="original">Enviada o recibida.</param>
    /// <param name="hoy">El día de la anulación.</param>
    /// <returns>El inverso, ya confirmado, y lo que movió.</returns>
    internal (Transferencia Inverso, LoQueMueveLaTransferencia Movido) Anular(Transferencia original, DateOnly hoy)
    {
        Transferencia inverso = original.CrearInverso(hoy, "Se mandó al almacén equivocado", Momento);

        IReadOnlyList<LineaValorada>? enElDestino = original.Estado == EstadoDeTransferencia.Recibida
            ? LaValoracion.DeLasLineas(_filas, inverso.LineasAValorarEnElDestino(), Divisa, hoy)
            : null;

        IReadOnlyList<LineaValorada> enElOrigen = LaValoracion.DeLasLineas(
            _filas, inverso.LineasAValorarEnElOrigen(enElDestino), Divisa, hoy);

        LoQueMueveLaTransferencia movido = Anotar(inverso.ConfirmarComoInverso(
            original,
            ++_numero,
            LaRecepcionDe(inverso, hoy),
            enElDestino,
            enElOrigen,
            LotesYSeriesResueltos.Ninguno,
            Momento));

        original.Anular(inverso, new TransferenciaAnulada(original.Id, Empresa));

        return (inverso, movido);
    }

    /// <summary>Lo que hay de la clave en el libro, en unidad base.</summary>
    /// <param name="almacen">El almacén de la clave.</param>
    /// <returns>La suma de las filas.</returns>
    internal decimal FisicoEn(Guid almacen) =>
        _filas.Where(fila => fila.AlmacenId == almacen).Sum(fila => fila.CantidadEnUnidadBase);

    /// <summary>El valor de la clave, la suma del valor de sus filas.</summary>
    /// <param name="almacen">El almacén de la clave.</param>
    /// <returns>La suma.</returns>
    internal decimal ValorEn(Guid almacen) =>
        _filas.Where(fila => fila.AlmacenId == almacen).Sum(fila => fila.Valor.Cantidad);

    /// <summary>Lo que vuela hacia el almacén, en unidad base.</summary>
    /// <param name="almacen">El almacén de destino.</param>
    /// <returns>La suma del tránsito.</returns>
    internal decimal EnTransitoHacia(Guid almacen) =>
        _transito.Where(fila => fila.AlmacenId == almacen).Sum(fila => fila.Cantidad);

    /// <summary>El valor de lo que vuela hacia el almacén.</summary>
    /// <param name="almacen">El almacén de destino.</param>
    /// <returns>La suma del valor en tránsito.</returns>
    internal decimal ValorEnTransitoHacia(Guid almacen) =>
        _transito.Where(fila => fila.AlmacenId == almacen).Sum(fila => fila.Valor.Cantidad);

    /// <summary>El valor de la empresa: el de todas las claves más el que vuela (ADR-0053 §6).</summary>
    /// <returns>La suma.</returns>
    internal decimal ValorDeLaEmpresa() =>
        _filas.Sum(fila => fila.Valor.Cantidad) + _transito.Sum(fila => fila.Valor.Cantidad);

    /// <summary>El evento del envío, con lo que dice el documento.</summary>
    /// <param name="transferencia">La que se envía.</param>
    /// <returns>El evento.</returns>
    internal static TransferenciaEnviada ElEnvioDe(Transferencia transferencia) => new(
        transferencia.Id,
        Empresa,
        transferencia.AlmacenOrigenId,
        transferencia.AlmacenDestinoId,
        transferencia.FechaDeEnvio,
        transferencia.Lineas.Count);

    /// <summary>El evento de la recepción, con lo que dice el documento.</summary>
    /// <param name="transferencia">La que se recibe.</param>
    /// <param name="fecha">El día en que llega.</param>
    /// <returns>El evento.</returns>
    internal static TransferenciaRecibida LaRecepcionDe(Transferencia transferencia, DateOnly fecha) => new(
        transferencia.Id, Empresa, transferencia.AlmacenDestinoId, fecha, transferencia.Lineas.Count);

    private LoQueMueveLaTransferencia Anotar(LoQueMueveLaTransferencia movido)
    {
        _filas.AddRange(movido.Movimientos);
        _transito.AddRange(movido.Transito);

        return movido;
    }
}
