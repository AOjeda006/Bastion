namespace Bastion.Organizacion.Domain.Empresas;

/// <summary>Régimen de IVA en el que tributa una empresa.</summary>
/// <remarks>
/// Se persiste como TEXTO y no como número: un enumerado guardado por su valor entero es un
/// dato que deja de significar nada en cuanto alguien reordena el enumerado, y en un ERP los
/// datos duran más que el código.
/// </remarks>
public enum RegimenDeIva
{
    /// <summary>Régimen general.</summary>
    General,

    /// <summary>Régimen simplificado (módulos).</summary>
    Simplificado,

    /// <summary>Recargo de equivalencia (comercio minorista).</summary>
    RecargoDeEquivalencia,

    /// <summary>Régimen especial del criterio de caja (RECC).</summary>
    CriterioDeCaja,

    /// <summary>Régimen especial de la agricultura, ganadería y pesca.</summary>
    AgriculturaGanaderiaYPesca,
}
