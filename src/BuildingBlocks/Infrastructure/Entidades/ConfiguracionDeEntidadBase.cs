using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.BuildingBlocks.Infrastructure.Entidades;

/// <summary>
/// Mapea las dos marcas de tiempo que toda <see cref="EntidadBase"/> lleva encima (R14).
/// </summary>
/// <remarks>
/// <para>
/// Las dos son <c>timestamptz</c> porque son <b>instantes</b>. La distinción con las fechas de
/// negocio —el inicio de un ejercicio, el devengo de una factura, que son <c>date</c>— la vigila
/// <c>LasFechasDicenDeQueTipoSonTests</c> sobre el modelo ya construido, y el esquema real la
/// confirma en la teoría de <c>information_schema</c>.
/// </para>
/// <para>
/// <b>Ninguna de las dos lleva <c>DEFAULT now()</c>.</b> Ataría las columnas al reloj del servidor
/// de base de datos —el único que una prueba no puede adelantar— y metería una forma nueva de
/// valor generado por el servidor en un modelo donde lo único que genera el servidor son los seis
/// testigos de concurrencia del ADR-0015. La hora sale del <c>TimeProvider</c> inyectado: la de
/// creación la pone la fábrica del dominio, la de modificación el interceptor de marcas.
/// </para>
/// </remarks>
public static class ConfiguracionDeEntidadBase
{
    /// <summary>Aplica el mapeo de las dos marcas.</summary>
    /// <typeparam name="T">La entidad, que hereda de <see cref="EntidadBase"/>.</typeparam>
    /// <param name="entidad">El constructor de la entidad.</param>
    public static void Mapear<T>(EntityTypeBuilder<T> entidad)
        where T : EntidadBase
    {
        ArgumentNullException.ThrowIfNull(entidad);

        // Se audita: no cambia nunca después del alta, así que un cambio suyo es exactamente el
        // tipo de cosa que la traza existe para poder contar.
        entidad.Property(fila => fila.CreadoEn).IsRequired().SeAudita();

        // NO se audita: cambia en TODAS las modificaciones, y el instante de cada una ya viaja en
        // la columna `ocurrido_en` de la propia fila de traza. Auditarla escribiría dos veces la
        // misma marca de tiempo en cada cambio del sistema, y la segunda no añade nada.
        entidad.Property(fila => fila.ModificadoEn)
            .IsRequired()
            .NoSeAudita("cambia en cada modificación y la traza ya lleva su propio `ocurrido_en`");
    }
}
