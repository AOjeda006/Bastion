using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Direcciones;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Terceros.Domain.Terceros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Terceros.Infrastructure.Persistencia.Configuraciones;

internal sealed class ConfiguracionDeTercero : IEntityTypeConfiguration<Tercero>
{
    public void Configure(EntityTypeBuilder<Tercero> tercero)
    {
        ArgumentNullException.ThrowIfNull(tercero);

        // LAS DOS RESTRICCIONES DE LA FICHA, en el motor y declaradas en el modelo —o sea, no
        // escritas a mano en la migración como el índice de la identificación—, porque estas SÍ
        // caben en lo que EF Core sabe expresar.
        //
        // La del territorio es la lista cerrada del §7.2. La columna guarda el enumerado como
        // texto, y sin CHECK un `UPDATE` a mano podría dejar «canarias» en minúscula, o
        // «Canaria», y el valor dejaría de convertirse al leerlo. Los cinco van escritos aquí a
        // partir de `Enum.GetNames`, no copiados: añadir un sexto territorio y olvidar el CHECK
        // sería exactamente el fallo que este CHECK existe para no tener.
        //
        // La del límite de crédito es que sus dos columnas van juntas o no van: una cantidad sin
        // divisa no es una cantidad de dinero, y una divisa sin cantidad no es nada. El dominio ya
        // lo sostiene —es un `Importe`, que no existe sin divisa—, y esto es lo que queda cuando
        // no hay proceso: un `UPDATE` a mano, una carga masiva, una restauración.
        tercero.ToTable("terceros", tabla =>
        {
            tabla.HasCheckConstraint(
                "ck_terceros_territorio_fiscal",
                "territorio_fiscal IN ('" +
                string.Join("', '", Enum.GetNames<TerritorioFiscal>()) + "')");

            tabla.HasCheckConstraint(
                "ck_terceros_limite_credito_completo",
                "(limite_credito_cantidad IS NULL) = (limite_credito_divisa IS NULL)");
        });

        // Se audita entero, y con más motivo que ningún otro maestro: la ficha puede contener el
        // nombre, el NIF y el domicilio de una persona física. Quién la dio de alta, quién le
        // cambió el domicilio y quién la bloqueó son las preguntas que hay que poder contestar.
        tercero.SeAudita();
        tercero.HasKey(fila => fila.Id);

        tercero.LlevaTestigoDeConcurrencia();

        tercero.Property(fila => fila.EmpresaId).IsRequired().SeAudita();

        // La identificación fiscal, como TIPO COMPLEJO: tres columnas en esta misma tabla. No es
        // un tipo poseído porque no tiene identidad propia —es un valor— y un poseído haría que
        // EF Core le sintetizara una clave y lo siguiera como si fuera una entidad (ADR-0016).
        tercero.ComplexProperty(fila => fila.Identificacion, identificacion =>
        {
            identificacion.IsRequired();

            identificacion.Property(campo => campo.Pais)
                .HasColumnName("identificacion_pais")
                .HasMaxLength(IdentificacionFiscal.LongitudDelPais)
                .IsRequired()
                .SeAudita();

            identificacion.Property(campo => campo.Numero)
                .HasColumnName("identificacion_numero")
                .HasMaxLength(IdentificacionFiscal.LongitudMaximaDelNumero)
                .IsRequired()
                .SeAudita();

            // Como TEXTO, igual que los demás enumerados: guardado por su valor entero dejaría de
            // significar nada en cuanto alguien reordenara el enumerado, y aquí eso importa de
            // más porque va a crecer con un tercer valor cuando exista la consulta al VIES.
            identificacion.Property(campo => campo.Verificacion)
                .HasColumnName("identificacion_verificacion")
                .HasConversion<string>()
                .IsRequired()
                .SeAudita();
        });

        // `text`: la razón social no tiene tope legal, y en PostgreSQL un `varchar(n)` inventado
        // no gana nada. El tope de 120 del dominio es el del diseño de registro de la AEAT y lo
        // impone la entidad, que es donde hay algo que decir sobre por qué son 120.
        tercero.Property(fila => fila.RazonSocial).IsRequired().SeAudita();
        tercero.Property(fila => fila.NombreComercial).SeAudita();

        tercero.Property(fila => fila.EsCliente).IsRequired().SeAudita();
        tercero.Property(fila => fila.EsProveedor).IsRequired().SeAudita();

        // El régimen fiscal, otro TIPO COMPLEJO: cuatro columnas en la misma fila, porque el
        // régimen no tiene identidad ni vida propia — es un rasgo de la ficha.
        tercero.ComplexProperty(fila => fila.RegimenFiscal, regimen =>
        {
            regimen.IsRequired();

            regimen.Property(campo => campo.Territorio)
                .HasColumnName("territorio_fiscal")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired()
                .SeAudita();

            regimen.Property(campo => campo.RecargoDeEquivalencia)
                .HasColumnName("recargo_de_equivalencia")
                .IsRequired()
                .SeAudita();

            regimen.Property(campo => campo.CriterioDeCaja)
                .HasColumnName("criterio_de_caja")
                .IsRequired()
                .SeAudita();

            regimen.Property(campo => campo.SujetoARetencionIrpf)
                .HasColumnName("sujeto_a_retencion_irpf")
                .IsRequired()
                .SeAudita();
        });

        // EL LÍMITE DE CRÉDITO: DOS COLUMNAS, Y LA DIVISA ES UNA DE ELLAS.
        //
        // `numeric(18,4)`, que es la escala de importe de R6: nunca coma flotante —un céntimo
        // perdido en una suma es un asiento que no cuadra— y nunca el `money` del motor, que lleva
        // pegada una configuración regional y desborda antes.
        //
        // Las dos son ANULABLES a la vez: no fiarle nada a un tercero no es un límite de cero, es
        // no tener límite. Que no se separen —cantidad sin divisa, o al revés— lo sostiene el
        // CHECK que va en la migración, porque una de las dos sola no significa nada.
        tercero.ComplexProperty(fila => fila.LimiteCredito, limite =>
        {
            limite.IsRequired(false);

            limite.Property(campo => campo.Cantidad)
                .HasColumnName("limite_credito_cantidad")
                .HasPrecision(18, Importe.Decimales)
                .SeAudita();

            limite.Property(campo => campo.Divisa)
                .HasColumnName("limite_credito_divisa")
                .HasMaxLength(3)
                .SeAudita();
        });

        ConfiguracionDeEntidadBase.Mapear(tercero);

        tercero.ComplexProperty(fila => fila.Bloqueo, ConfiguracionDeBloqueo.Mapear);

        // Domicilio fiscal OBLIGATORIO, a diferencia de la dirección de un almacén: sin él no se
        // puede emitir una factura a nombre de este tercero, que es para lo que existe la ficha.
        tercero.ComplexProperty(fila => fila.DomicilioFiscal, domicilio =>
        {
            domicilio.IsRequired();
            ConfiguracionDeDireccion.Mapear(domicilio);
        });

        // LA UNICIDAD DE (EMPRESA, IDENTIFICADOR) NO ESTÁ AQUÍ, Y HAY QUE LEER POR QUÉ.
        //
        // Está en la migración, escrita con `CreateIndex` sobre las tres columnas, porque EF Core
        // 10 NO sabe indexar propiedades de un tipo complejo: ni con el selector —«no es una
        // expresión de acceso a miembro válida»— ni con los nombres —«la propiedad
        // "Identificacion.Pais" no se puede añadir al tipo "Tercero"»—. Comprobado ejecutando las
        // dos formas, no supuesto.
        //
        // La alternativa era degradar la identificación a tipo poseído, que SÍ admite índices, y no
        // se ha hecho: un poseído tiene identidad sintetizada y EF lo sigue como una entidad más
        // (ADR-0016). Se prefiere que el mapeo diga la verdad sobre el modelo y que la restricción
        // viva donde de todos modos la aplica el motor.
        //
        // Lo que eso obliga a hacer, y está hecho: como el modelo no conoce el índice, no hay
        // manera de que `has-pending-model-changes` lo eche en falta. Quien lo comprueba es el
        // test de esquema, que lo busca EN LA BASE y además afirma que no tiene predicado parcial
        // —que es la decisión del ítem 1.5, no un detalle del índice—.

        // LO QUE CUELGA DE LA FICHA, con borrado en cascada y desde el campo, no desde la
        // propiedad de solo lectura: la colección la gobierna el agregado, y `UsePropertyAccessMode
        // .Field` es lo que impide que EF intente escribir en un `IReadOnlyList` que no tiene
        // asignador. La cascada es la correcta porque un contacto sin ficha no es nada — no es un
        // recurso que sobreviva a su padre, como sí lo es una factura.
        tercero.HasMany(fila => fila.Contactos)
            .WithOne()
            .HasForeignKey(contacto => contacto.TerceroId)
            .OnDelete(DeleteBehavior.Cascade);

        tercero.Navigation(fila => fila.Contactos).UsePropertyAccessMode(PropertyAccessMode.Field);

        tercero.HasMany(fila => fila.CuentasBancarias)
            .WithOne()
            .HasForeignKey(cuenta => cuenta.TerceroId)
            .OnDelete(DeleteBehavior.Cascade);

        tercero.Navigation(fila => fila.CuentasBancarias)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        tercero.HasMany(fila => fila.CondicionesPago)
            .WithOne()
            .HasForeignKey(condicion => condicion.TerceroId)
            .OnDelete(DeleteBehavior.Cascade);

        tercero.Navigation(fila => fila.CondicionesPago)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Sin clave ajena a `organizacion.empresas`, y no por olvido: la empresa vive en otro
        // esquema y la regla 4 no deja cruzarlos. Lo que impide que aquí acabe el identificador de
        // una empresa inventada —o dada de baja— es `IConsultaDeEmpresas`, que el alta pregunta
        // antes de crear la ficha.
    }
}
