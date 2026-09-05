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

        tercero.ToTable("terceros");

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

        // Sin clave ajena a `organizacion.empresas`, y no por olvido: la empresa vive en otro
        // esquema y la regla 4 no deja cruzarlos. Lo que impide que aquí acabe el identificador de
        // una empresa inventada —o dada de baja— es `IConsultaDeEmpresas`, que el alta pregunta
        // antes de crear la ficha.
    }
}
