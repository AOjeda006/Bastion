using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.BuildingBlocks.Infrastructure.Direcciones;

/// <summary>
/// Mapea una <see cref="Direccion"/> como <b>tipo complejo</b>: seis columnas en la tabla del
/// dueño, una por campo (R17).
/// </summary>
/// <remarks>
/// <para>
/// Está en un solo sitio porque la usan la empresa, el almacén y —desde el ítem 1.5— el tercero,
/// y porque los topes son los del rulebook de SEPA: repetirlos por entidad sería garantizar que un
/// día dejan de coincidir. Vive en el bloque común por lo mismo que <c>ConfiguracionDeBloqueo</c>:
/// hasta el 1.5 sus dos dueños eran del mismo módulo y podía quedarse dentro; el tercero es de
/// otro, y una copia en Terceros sería el segundo sitio donde el nombre de una columna puede
/// divergir.
/// </para>
/// <para>
/// <b>Era un tipo poseído hasta el 0.10.</b> Un objeto de valor no tiene identidad, y un tipo
/// poseído sí —EF Core le sintetiza una clave, lo sigue como una entidad más y lo saca en
/// <c>GetEntityTypes()</c>—: el mapeo decía de la dirección algo que el dominio niega. El cambio
/// resultó ser <b>neutro para el esquema</b> —las mismas seis columnas, los mismos topes, ninguna
/// migración pendiente—, así que lo único que estaba en juego era decir la verdad sobre el modelo.
/// </para>
/// <para>
/// Lo que NO era neutro es quién mira: las propiedades de un tipo complejo no salen en
/// <c>GetProperties()</c> ni en <c>entrada.Properties</c>. Antes de hacer este cambio se
/// ampliaron los barridos y el interceptor de auditoría para que recorran también los tipos
/// complejos; hacerlo al revés habría sacado doce propiedades de la clasificación con todo en
/// verde. Está contado en el ADR-0016.
/// </para>
/// </remarks>
public static class ConfiguracionDeDireccion
{
    /// <summary>Aplica el mapeo de los seis campos y sus topes.</summary>
    public static void Mapear(ComplexPropertyBuilder<Direccion> direccion)
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
