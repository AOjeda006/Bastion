using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Recuentos;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.UnitTests.Recuentos;

/// <summary>Lo que los casos del recuento necesitan para montar uno, sin repetirlo en cada fichero.</summary>
internal static class ElRecuentoDeLaPrueba
{
    internal static readonly DateTimeOffset Momento = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    internal static readonly DateOnly Apertura = new(2026, 10, 6);

    /// <summary>Un día después de abrirlo: contar lleva tiempo, y las dos fechas no son la misma.</summary>
    internal static readonly DateOnly Confirmacion = new(2026, 10, 7);

    internal static readonly Guid Estanteria = Guid.CreateVersion7();

    internal static readonly Guid OtraEstanteria = Guid.CreateVersion7();

    internal static readonly Guid Tornillos = Guid.CreateVersion7();

    internal static readonly Guid Tuercas = Guid.CreateVersion7();

    internal static readonly Guid Taladros = Guid.CreateVersion7();

    internal static readonly Guid Unidades = Guid.CreateVersion7();

    internal const string Motivo = "Recuento de octubre del almacén central";

    internal static Recuento Abrir(params LineaAPrecargar[] precarga) =>
        Recuento.Abrir(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Apertura,
            Motivo,
            "EUR",
            precarga,
            Momento);

    internal static LineaAPrecargar Precargada(
        Guid ubicacionId,
        Guid articuloId,
        string? lote = null,
        string? serie = null) =>
        new(ClaveDelRecuento.De(ubicacionId, articuloId, lote, serie), Unidades);

    /// <summary>La línea que lleva esa clave. Que exista una y solo una lo afirma quien la busca.</summary>
    internal static LineaDeRecuento LaDe(Recuento recuento, Guid articuloId, string? serie = null) =>
        recuento.Lineas.Single(linea => linea.ArticuloId == articuloId && linea.NumeroDeSerie == serie);

    /// <summary>El teórico de cada línea, con el mismo valor para todas.</summary>
    internal static Dictionary<Guid, decimal> TodasA(Recuento recuento, decimal teorico) =>
        recuento.Lineas.ToDictionary(linea => linea.Id, _ => teorico);

    /// <summary>
    /// Confirma el ajuste que genera el recuento, como haría el caso de uso con lo que bloquea: cada
    /// línea vale dos euros la unidad, y cada lote y cada serie tienen su fila.
    /// </summary>
    internal static void ConfirmarSuAjuste(Ajuste ajuste, long numero = 9)
    {
        IReadOnlyList<LineaValorada> valoradas =
        [
            .. ajuste.LineasAValorar().Select(linea => new LineaValorada(
                Importe.De(linea.Cantidad * 2m, ajuste.Divisa), PrecioUnitario.De(2m, ajuste.Divisa))),
        ];

        var resueltos = new LotesYSeriesResueltos(
            ajuste.LotesQueNombra().ToDictionary(lote => lote, _ => Guid.CreateVersion7()),
            ajuste.SeriesQueNombra().ToDictionary(serie => serie, _ => Guid.CreateVersion7()));

        _ = ajuste.Confirmar(
            numero,
            new AjusteConfirmado(ajuste.Id, ajuste.EmpresaId, ajuste.AlmacenId, ajuste.FechaDeOperacion, ajuste.Lineas.Count),
            valoradas,
            resueltos,
            Momento);
    }

    /// <summary>
    /// Cuenta cada línea con lo que diga <paramref name="contado"/>, la confirma contra ese teórico y
    /// devuelve el ajuste que generó, ya confirmado, o <c>null</c> si todo cuadraba.
    /// </summary>
    internal static Ajuste? ContarYConfirmar(
        Recuento recuento,
        IReadOnlyDictionary<Guid, decimal> teoricos,
        Func<LineaDeRecuento, decimal> contado,
        long numero = 31)
    {
        foreach (LineaDeRecuento linea in recuento.Lineas)
        {
            recuento.Contar(linea.Id, contado(linea), teoricos[linea.Id]);
        }

        Ajuste? ajuste = recuento.AjusteDeLaDiferencia(teoricos, Confirmacion, Momento);

        if (ajuste is not null)
        {
            ConfirmarSuAjuste(ajuste);
        }

        recuento.Confirmar(numero, Confirmacion, teoricos, ajuste, Confirmado(recuento, ajuste));

        return ajuste;
    }

    internal static RecuentoConfirmado Confirmado(Recuento recuento, Ajuste? ajuste) =>
        new(recuento.Id, recuento.EmpresaId, recuento.AlmacenId, Confirmacion, recuento.Lineas.Count, ajuste?.Id);

    internal static RecuentoAnulado Anulado(Recuento recuento) =>
        new(recuento.Id, recuento.EmpresaId, recuento.AlmacenId);

    internal static RecuentoDescartado Descartado(Recuento recuento) =>
        new(recuento.Id, recuento.EmpresaId, recuento.AlmacenId);
}
