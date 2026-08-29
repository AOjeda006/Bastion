using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Mapea una <see cref="Direccion"/> como tipo poseído: seis columnas en la tabla del dueño,
/// una por campo (R17).
/// </summary>
/// <remarks>
/// Está en un solo sitio porque la usan la empresa y el almacén, y porque los topes son los del
/// rulebook de SEPA: repetirlos por entidad sería garantizar que un día dejan de coincidir.
/// </remarks>
internal static class ConfiguracionDeDireccion
{
    /// <summary>Aplica el mapeo de los seis campos y sus topes.</summary>
    public static void Mapear<T>(OwnedNavigationBuilder<T, Direccion> direccion)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(direccion);

        direccion.Property(campo => campo.Calle)
            .HasMaxLength(Direccion.LongitudMaximaDeCalle)
            .IsRequired()
            .SeAudita();

        direccion.Property(campo => campo.Numero)
            .HasMaxLength(Direccion.LongitudMaximaDeNumero)
            .SeAudita();

        direccion.Property(campo => campo.CodigoPostal)
            .HasMaxLength(Direccion.LongitudMaximaDeCodigoPostal)
            .IsRequired()
            .SeAudita();

        direccion.Property(campo => campo.Poblacion)
            .HasMaxLength(Direccion.LongitudMaximaDePoblacion)
            .IsRequired()
            .SeAudita();

        direccion.Property(campo => campo.Subdivision)
            .HasMaxLength(Direccion.LongitudMaximaDeSubdivision)
            .SeAudita();

        direccion.Property(campo => campo.Pais)
            .HasMaxLength(Direccion.LongitudDelPais)
            .IsRequired()
            .SeAudita();
    }
}
