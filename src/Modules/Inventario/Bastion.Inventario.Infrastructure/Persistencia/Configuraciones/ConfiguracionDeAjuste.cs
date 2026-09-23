using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Inventario.Domain.Ajustes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeAjuste : IEntityTypeConfiguration<Ajuste>
{
    public void Configure(EntityTypeBuilder<Ajuste> ajuste)
    {
        ArgumentNullException.ThrowIfNull(ajuste);

        ajuste.ToTable("ajustes");
        ajuste.HasKey(documento => documento.Id);

        // EL DOCUMENTO SÍ SE AUDITA, y es la otra mitad de por qué el libro no. Aquí es donde hay
        // decisiones de una persona —qué se ajusta, por qué, cuándo se confirma— y una traza de
        // esos cambios contesta preguntas que la fila del libro no contesta.
        ajuste.SeAudita();

        // Y LLEVA TESTIGO, que es la diferencia con sus líneas y con el libro: es un agregado que
        // CAMBIA. La transición a `Confirmado` lee el estado y luego escribe, y sin testigo dos
        // confirmaciones simultáneas del mismo ajuste leerían las dos `Borrador` y escribirían las
        // dos su tanda de movimientos — el stock movido dos veces, sin error y sin rastro. No
        // cuesta una columna: el testigo es `xmin`, que PostgreSQL ya lleva en toda fila.
        ajuste.LlevaTestigoDeConcurrencia();

        ConfiguracionDeEntidadBase.Mapear(ajuste);

        ajuste.Property(documento => documento.EmpresaId).IsRequired().SeAudita();
        ajuste.Property(documento => documento.AlmacenId).IsRequired().SeAudita();

        // SIN CLAVE AJENA a `organizacion.series`, como los otros cuatro identificadores de este
        // documento: ninguna clave ajena cruza de esquema (§5, regla 4). Lo que comprueba que la
        // serie sirve es el `WHERE` de la sentencia que numera, en el instante de numerar.
        ajuste.Property(documento => documento.SerieId).IsRequired().SeAudita();

        // NULO MIENTRAS SEA BORRADOR, y por eso no es `IsRequired()`. Se audita porque es el dato
        // que hace falta para reconstruir por qué un correlativo es el que es.
        ajuste.Property(documento => documento.Numero).SeAudita();

        // `date`, no `timestamptz`: es una fecha de NEGOCIO (R14), y además es la que acabará
        // decidiendo en qué partición del libro caen las filas de este documento.
        ajuste.Property(documento => documento.FechaDeOperacion).IsRequired().SeAudita();

        ajuste.Property(documento => documento.Motivo)
            .HasMaxLength(Ajuste.LargoDelMotivo)
            .IsRequired()
            .SeAudita();

        // Como TEXTO, igual que los demás enumerados del proyecto: un entero en la base obliga a
        // tener el código delante para leer una fila. Y se audita, que es lo que convierte la
        // máquina de estados en algo que se puede contar después: quién confirmó y cuándo.
        ajuste.Property(documento => documento.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .SeAudita();

        // La colección se carga y se guarda con el documento: es su agregado. Los MOVIMIENTOS no
        // están aquí a propósito —viven fuera, con su motivo en `Ajuste.Confirmar`—, y por eso
        // cargar un ajuste para confirmarlo no trae ni una fila del libro al rastreador.
        ajuste.HasMany(documento => documento.Lineas)
            .WithOne()
            .HasForeignKey(linea => linea.AjusteId)
            .OnDelete(DeleteBehavior.Cascade);

        ajuste.Navigation(documento => documento.Lineas).AutoInclude();

        // EL ENLACE DEL PAR, y es UNA columna en el inverso, no una en cada mitad. Dos columnas
        // —«a quién anulo» aquí y «quién me anula» en el original— son dos sitios donde guardar el
        // mismo hecho, y acaban discrepando; con esta sola, la flecha se recorre igual en los dos
        // sentidos: hacia el original por el identificador, y desde el original buscando quién le
        // apunta.
        ajuste.Property(documento => documento.AnulaAId).SeAudita();

        // CLAVE AJENA SÍ, y es la única de este documento: apunta a la MISMA tabla y al mismo
        // esquema, así que no cruza ninguna frontera (§5, regla 4) — lo que impedía las otras
        // cuatro era el esquema ajeno, no un desprecio por la integridad referencial. `Restrict` y
        // no `Cascade`: borrar el original no puede llevarse por delante al documento que lo
        // compensa (ADR-0007 §8).
        ajuste.HasOne<Ajuste>()
            .WithMany()
            .HasForeignKey(documento => documento.AnulaAId)
            .OnDelete(DeleteBehavior.Restrict);

        // ÍNDICE ÚNICO SOBRE `anula_a_id`: dos inversos del mismo original no entran en la base.
        //
        // AQUÍ HABÍA UN ÍNDICE NO ÚNICO, y el argumento para dejarlo así era que el único chocaría
        // PRIMERO en la carrera —dos anulaciones simultáneas INSERTAN su inverso antes de tocar el
        // original— y la perdedora saldría por una violación de unicidad, un `500`, en vez de por
        // el testigo de concurrencia, que es un `412`. La medición era cierta; la conclusión, no.
        // Cambiaba una GARANTÍA por un CÓDIGO DE ESTADO, y las dos se pueden tener: la respuesta
        // se arregla traduciendo, y `ManejadorDeCarreraPerdidaEnLaBase` traduce ESTE índice, por
        // su nombre, al mismo `412` que da el testigo. Que el nombre siga existiendo y siga
        // siendo ÚNICO lo compara `CadaIndiceTraducidoSeJustificaTests`, en los dos sentidos.
        //
        // Y el contraargumento estaba cuatro líneas más abajo, en esta misma tabla: el único de
        // `(serie_id, numero)` se justifica diciendo que NO duplica al cerrojo, sino que es «la que
        // queda en pie cuando el cerrojo no interviene —una carga, una corrección a mano, un
        // documento nuevo que copie mal el patrón—». Eso vale palabra por palabra para el inverso:
        // que no haya dos depende hoy de que TODO camino que cree uno toque también la fila del
        // original, y el 2.12 y la fase 5 traen caminos que aún no existen. El barrido de
        // `LaDobleFlechaDeLaAnulacionTests` vigila la base de los tests; este índice vigila la de
        // producción.
        //
        // LO QUE EL ÍNDICE SIGUE SIN PODER VER, y por eso el barrido no sobra: un inverso cuyo
        // original NO está anulado. Eso es una condición sobre el ESTADO de otra fila, y no cabe
        // en ninguna restricción de columna.
        //
        // FILTRADO por el mismo motivo que el de `numero`, y aquí pesa más: la inmensa mayoría de
        // los ajustes no anulan a nadie, así que sin filtro el índice indexaría una tabla entera de
        // nulos para vigilar a la minoría que apunta.
        ajuste.HasIndex(documento => documento.AnulaAId)
            .IsUnique()
            .HasFilter("anula_a_id IS NOT NULL");

        ajuste.HasIndex(documento => new { documento.EmpresaId, documento.FechaDeOperacion });

        // LA ÚNICA MITAD DE LA R5 QUE PUEDE VIVIR EN ESTA TABLA: «correlativa» prohibe que dos
        // documentos de la misma serie lleven el mismo número, y eso sí se comprueba con las
        // columnas que hay aquí. «Sin huecos» no: un hueco es una fila que NO existe, y ningún
        // índice habla de filas que no existen —de eso responde el mecanismo de numeración—.
        //
        // NO ES UNA COMPROBACIÓN DUPLICADA de lo que ya garantiza el cerrojo: es la que queda en
        // pie cuando el cerrojo no interviene. La sentencia protege contra dos confirmaciones
        // simultáneas; este índice protege contra cualquier camino que en el futuro escriba
        // `numero` sin pasar por ella —una carga, una corrección a mano, un documento nuevo que
        // copie mal el patrón—, y contra esos el cerrojo no dice nada.
        //
        // FILTRADO POR `numero IS NOT NULL` en vez de fiarlo a que PostgreSQL considere distintos
        // los nulos: ese comportamiento es el que trae por defecto, se puede cambiar en la
        // definición del índice, y dejar la corrección de los borradores colgando de un ajuste por
        // omisión es lo que no se lee al revisar. Además deja fuera del índice a los borradores,
        // que son justo las filas que más cambian.
        ajuste.HasIndex(documento => new { documento.SerieId, documento.Numero })
            .IsUnique()
            .HasFilter("numero IS NOT NULL");
    }
}
