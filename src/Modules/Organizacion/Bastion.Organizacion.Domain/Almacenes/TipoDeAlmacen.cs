namespace Bastion.Organizacion.Domain.Almacenes;

/// <summary>Naturaleza de un almacén.</summary>
public enum TipoDeAlmacen
{
    /// <summary>Existe en un sitio y tiene dirección.</summary>
    Fisico,

    /// <summary>Contrapartida contable de regularizaciones y ajustes; no está en ningún sitio.</summary>
    Virtual,

    /// <summary>Mercancía en camino entre dos almacenes físicos.</summary>
    Transito,
}
