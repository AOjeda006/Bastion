namespace Bastion.Organizacion.Domain.Series;

/// <summary>Estado de una serie documental.</summary>
/// <remarks>
/// Tampoco lleva «bloqueado»: una serie no contiene datos personales. Lo que sí tiene es un
/// final de vida legal —dejar de numerar sin perder el histórico—, y eso es <c>Cerrada</c>.
/// </remarks>
public enum EstadoDeSerie
{
    /// <summary>Puede seguir asignando números.</summary>
    Activa,

    /// <summary>No asigna más números; el contador se conserva como está.</summary>
    Cerrada,
}
