using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.Organizacion.Domain.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeContadorDeSerie : IEntityTypeConfiguration<ContadorDeSerie>
{
    /// <summary>La tabla, nombrada aquí porque la sentencia de numeración la escribe en crudo.</summary>
    internal const string Tabla = "contadores_de_serie";

    /// <summary>Su esquema, por lo mismo.</summary>
    internal const string Esquema = OrganizacionDbContext.Esquema;

    public void Configure(EntityTypeBuilder<ContadorDeSerie> contador)
    {
        ArgumentNullException.ThrowIfNull(contador);

        contador.ToTable(Tabla, Esquema);

        // LA CLAVE ES `serie_id`, no una clave propia: una serie tiene un contador y solo uno, y
        // que la unicidad la imponga la clave primaria evita la tabla que más daño haría de todas
        // —la que tiene dos filas para la misma serie, cada una con un último número distinto—.
        contador.HasKey(fila => fila.SerieId);

        // SU PROPIO TESTIGO, y no por simetría con el de la serie: es lo que sostiene la carrera
        // suprimir-contra-confirmar. Hasta el ADR-0039 la sostenía el `xmin` de `series` —numerar
        // movía esa fila y `EliminarSerie` exige la versión—, y al salir el contador de allí ese
        // argumento se queda sin base. Ahora quien numera mueve ESTE `xmin`, y el `DELETE` que el
        // ORM arrastra al borrar la serie lo lleva dentro.
        contador.LlevaTestigoDeConcurrencia();

        contador.Property(fila => fila.SerieId).IsRequired();
        contador.Property(fila => fila.UltimoNumero).IsRequired().HasDefaultValue(0L);

        // NO SE AUDITA, y el motivo no es que el número dé igual: es que el interceptor NO LO
        // VERÍA. Esta columna la escribe una sentencia cruda dentro de la transacción de quien
        // confirma —es la única manera de tomar el cerrojo sobre la fila—, y el SQL a mano no pasa
        // por el rastreador de cambios. Un `SeAudita()` aquí sería una promesa que el mecanismo no
        // puede cumplir, y una traza que dijera menos de lo que promete es peor que ninguna.
        //
        // Lo que sí queda auditado es el DOCUMENTO que se lleva el número, con su serie dentro.
        contador.NoSeAudita(
            "lo escribe la sentencia de numeración en crudo, que no pasa por el rastreador de " +
            "cambios: auditarlo sería prometer una traza que el mecanismo no puede escribir. El " +
            "rastro del número es el documento que lo lleva");
    }
}
