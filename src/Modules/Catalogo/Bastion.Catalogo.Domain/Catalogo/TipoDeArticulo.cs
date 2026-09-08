namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>Qué clase de cosa es lo que se vende o se compra.</summary>
/// <remarks>
/// <para>
/// La distinción no es de presentación: un <see cref="Bien"/> se almacena y se mueve, y a partir
/// de la fase 2 tendrá existencias y movimientos; un <see cref="Servicio"/> no tiene ni una cosa
/// ni la otra, y pedirle un almacén sería pedirle un dato que no existe.
/// </para>
/// <para>
/// Se guarda como <b>texto</b> y no como entero, igual que los demás enumerados del sistema: un
/// enumerado guardado por su valor deja de significar nada en cuanto alguien reordena las
/// constantes, y en un ERP los datos duran más que el código.
/// </para>
/// </remarks>
public enum TipoDeArticulo
{
    /// <summary>Mercancía: se almacena, se mueve y se cuenta.</summary>
    Bien = 0,

    /// <summary>Prestación: se factura, pero no hay nada que guardar en un almacén.</summary>
    Servicio = 1,
}
