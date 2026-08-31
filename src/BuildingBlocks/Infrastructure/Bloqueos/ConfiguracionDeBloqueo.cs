using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.BuildingBlocks.Infrastructure.Bloqueos;

/// <summary>
/// Mapea un <see cref="Bloqueo"/> como tipo complejo: tres columnas en la tabla de la entidad
/// bloqueable (R16).
/// </summary>
/// <remarks>
/// <para>
/// Está en <c>BuildingBlocks</c> y no en un módulo porque lo usan Organización —empresa y
/// almacén— e Identidad —usuario—. Tres copias de este mapeo serían tres sitios donde el nombre
/// de la columna, el tipo del motivo o la obligatoriedad pueden divergir, y el día que diverjan
/// la consulta que responda «qué hay bloqueado en esta instalación» tendrá que escribirse tres
/// veces.
/// </para>
/// <para>
/// <b>Los nombres de las columnas se dicen a mano.</b> Por convención salen del nombre del
/// miembro, y aquí eso daría <c>bloqueo_esta_bloqueado</c> y <c>bloqueo_desde</c>: correctos y
/// feos. <c>bloqueado_en</c> además ya existía en dos de las tres tablas antes del 0.10, así que
/// mantenerlo ahorra una migración de datos y deja una sola grafía en las tres.
/// </para>
/// </remarks>
public static class ConfiguracionDeBloqueo
{
    /// <summary>Aplica el mapeo de las tres columnas.</summary>
    /// <param name="bloqueo">El constructor del tipo complejo.</param>
    public static void Mapear(ComplexPropertyBuilder<Bloqueo> bloqueo)
    {
        ArgumentNullException.ThrowIfNull(bloqueo);

        // Se audita: bloquear y desbloquear son, literalmente, los dos cambios que hay que poder
        // demostrar que se hicieron y cuándo.
        bloqueo.Property(campo => campo.EstaBloqueado)
            .HasColumnName("bloqueado")
            .IsRequired()
            .SeAudita();

        // Se audita: de esta fecha cuelga el plazo de prescripción del art. 32. Si alguna vez se
        // moviera, es exactamente lo que la traza tiene que poder contar.
        bloqueo.Property(campo => campo.Desde)
            .HasColumnName("bloqueado_en")
            .SeAudita();

        // Se audita, y va como TEXTO: guardado por su valor entero dejaría de significar nada en
        // cuanto alguien reordenara el enumerado, y los datos duran más que el código.
        bloqueo.Property(campo => campo.Motivo)
            .HasColumnName("motivo_del_bloqueo")
            .HasConversion<string>()
            .SeAudita();
    }
}
