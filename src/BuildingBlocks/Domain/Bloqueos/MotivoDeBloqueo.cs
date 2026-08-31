namespace Bastion.BuildingBlocks.Domain.Bloqueos;

/// <summary>
/// Por qué se bloqueó algo (R16).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lista cerrada y no texto libre</b>, por lo mismo que <c>MotivoSinInquilino</c> y
/// <c>MotivoDeRevocacion</c>: añadir un motivo obliga a tocar este enumerado, que es un cambio que
/// se ve en la revisión. Con un <c>string</c>, la columna se llenaría de «baja», «ok» y «pruebas»,
/// y la pregunta que de verdad importa —cuáles de estas filas están reservadas por el artículo 32
/// y cuáles no— dejaría de tener respuesta consultable.
/// </para>
/// <para>
/// <b>Dos valores, que son los dos caminos que hoy existen.</b> No hay un tercero de reserva: un
/// motivo que nadie produce es una rama que nadie prueba. Cuando aparezca un camino nuevo —la
/// destrucción al vencer el plazo, por ejemplo— traerá el suyo.
/// </para>
/// </remarks>
public enum MotivoDeBloqueo
{
    /// <summary>
    /// Procede la supresión de los datos (art. 32 de la LOPDGDD): se identifican y se reservan en
    /// vez de borrarse, y quedan fuera de todo tratamiento salvo para jueces, Fiscalía y
    /// Administraciones competentes mientras dure el plazo de prescripción. Es lo que significa un
    /// <c>DELETE</c> sobre una empresa o sobre un usuario.
    /// </summary>
    SupresionSolicitada,

    /// <summary>
    /// Deja de usarse, pero sus datos no son de nadie: un almacén que ya no admite movimientos.
    /// El histórico de valoración apunta a él para siempre, así que la ficha se conserva por una
    /// razón contable y no por una razón de protección de datos.
    /// </summary>
    CeseDeUso,
}
