namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>Cada cuánto mira el publicador la cola, y cuántos eventos se lleva por vuelta.</summary>
/// <remarks>
/// <para>
/// Dos segundos y cien eventos. El intervalo fija el retraso mínimo de una consistencia que ya es
/// eventual por diseño: bajarlo a milisegundos sería fingir que no lo es, y subirlo a minutos
/// convertiría «el asiento tarda un poco» en «el asiento no está». El tamaño acota lo que una
/// vuelta puede tardar en soltar el cerrojo.
/// </para>
/// <para>
/// Son valores del cableado y no configuración de despliegue: no hay variable de entorno para
/// ellos porque nadie ha necesitado todavía cambiarlos sin recompilar, y una opción configurable
/// sin caso de uso es una superficie que hay que documentar y probar. Los tests los bajan
/// construyendo el objeto.
/// </para>
/// </remarks>
public sealed record OpcionesDeLaBandeja
{
    /// <summary>Cada cuánto se mira la cola.</summary>
    public TimeSpan Intervalo { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Cuántos eventos se llevan por vuelta.</summary>
    public int Tamano { get; init; } = 100;
}
