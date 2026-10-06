using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Recuentos;
using Shouldly;
using static Bastion.Inventario.UnitTests.Recuentos.ElRecuentoDeLaPrueba;

namespace Bastion.Inventario.UnitTests.Recuentos;

/// <summary>
/// La máquina de estados del <c>Recuento</c> (R1, ADR-0055 §13): nace en curso, y de ahí sale
/// confirmado o descartado; un confirmado se anula, y nada más.
/// </summary>
/// <remarks>
/// <b>Cada transición imposible tiene su caso</b>, como en el ajuste: fallan por el mismo mecanismo,
/// pero un cambio en la tabla de transiciones del recuento rompería una sola, y el nombre del caso
/// dice cuál.
/// </remarks>
public sealed class LaMaquinaDeEstadosDelRecuentoTests
{
    [Fact]
    public void Un_recuento_nace_en_curso_sin_numero_y_sin_haber_contado_nada()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));

        recuento.Estado.ShouldBe(EstadoDeRecuento.EnCurso);

        recuento.Numero.ShouldBeNull(
            "se numera al confirmar (ADR-0055 §1.4): un recuento descartado no puede dejar un hueco");

        recuento.FechaDeConfirmacion.ShouldBeNull();
        recuento.FechaDeApertura.ShouldBe(Apertura);
        recuento.Motivo.ShouldBe(Motivo);
        recuento.Divisa.ShouldBe("EUR");

        recuento.EventosPendientes.ShouldBeEmpty(
            "abrir un recuento no es un hecho que nadie de fuera necesite: se cuentan sus transiciones");
    }

    [Fact]
    public void Confirmar_lo_numera_con_la_fecha_de_la_confirmacion_y_lo_cuenta()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));

        _ = ContarYConfirmar(recuento, TodasA(recuento, 5m), _ => 5m, numero: 31);

        recuento.Estado.ShouldBe(EstadoDeRecuento.Confirmado);
        recuento.Numero.ShouldBe(31);

        recuento.FechaDeConfirmacion.ShouldBe(
            Confirmacion,
            "el ajuste lleva la fecha de la confirmación, y el recuento la suya: es la que cuenta para el " +
            "ejercicio (ADR-0055 §1.5)");

        recuento.FechaDeApertura.ShouldBe(Apertura, "confirmar no mueve el día en que se abrió");
        recuento.EventosPendientes.Count.ShouldBe(1);
    }

    [Fact]
    public void Confirmar_dos_veces_no_se_puede()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        Dictionary<Guid, decimal> teoricos = TodasA(recuento, 5m);
        _ = ContarYConfirmar(recuento, teoricos, _ => 5m, numero: 31);

        Should.Throw<InvalidOperationException>(
            () => recuento.Confirmar(32, Confirmacion, teoricos, null, Confirmado(recuento, null)));

        recuento.Numero.ShouldBe(31, "la segunda confirmación no llega a repintar el número");
    }

    [Fact]
    public void Confirmar_antes_del_dia_en_que_se_abrio_no_se_puede()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        Dictionary<Guid, decimal> teoricos = TodasA(recuento, 5m);
        recuento.Contar(recuento.Lineas[0].Id, 5m, 5m);

        Should.Throw<ArgumentOutOfRangeException>(
            () => recuento.Confirmar(31, Apertura.AddDays(-1), teoricos, null, Confirmado(recuento, null)));

        recuento.Estado.ShouldBe(EstadoDeRecuento.EnCurso);
        recuento.Confirmar(31, Apertura, teoricos, null, Confirmado(recuento, null));
        recuento.FechaDeConfirmacion.ShouldBe(Apertura, "el mismo día sí: se puede contar en una mañana");
    }

    [Fact]
    public void Descartar_un_recuento_en_curso_lo_deja_descartado_con_su_motivo_y_sin_numero()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));

        recuento.Descartar("  Se contó el almacén equivocado  ", Descartado(recuento));

        recuento.Estado.ShouldBe(EstadoDeRecuento.Descartado);
        recuento.MotivoDelDescarte.ShouldBe("Se contó el almacén equivocado");
        recuento.Numero.ShouldBeNull("un descartado no se numera (ADR-0055 §1.6)");
        recuento.EventosPendientes.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Descartar_sin_motivo_no_se_puede(string motivo)
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));

        Should.Throw<ArgumentException>(() => recuento.Descartar(motivo, Descartado(recuento)));

        recuento.Estado.ShouldBe(EstadoDeRecuento.EnCurso);
    }

    [Fact]
    public void Un_motivo_de_descarte_mas_largo_que_el_del_alta_no_entra()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));

        Should.Throw<ArgumentException>(
            () => recuento.Descartar(new string('x', Recuento.LargoDelMotivo + 1), Descartado(recuento)));
    }

    [Fact]
    public void Descartar_un_confirmado_no_se_puede()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        _ = ContarYConfirmar(recuento, TodasA(recuento, 5m), _ => 5m);

        Should.Throw<InvalidOperationException>(
            () => recuento.Descartar("Ya no hace falta", Descartado(recuento)));
    }

    [Fact]
    public void Anular_un_recuento_en_curso_no_se_puede_porque_lo_que_no_movio_nada_se_descarta()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));

        Should.Throw<InvalidOperationException>(
            () => recuento.Anular("Me equivoqué", null, Anulado(recuento)));
    }

    [Fact]
    public void Anular_un_confirmado_que_no_movio_el_libro_solo_cambia_su_estado()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        Ajuste? ajuste = ContarYConfirmar(recuento, TodasA(recuento, 5m), _ => 5m);
        ajuste.ShouldBeNull("todo cuadraba");

        recuento.Anular("  Se contó dos veces  ", null, Anulado(recuento));

        recuento.Estado.ShouldBe(EstadoDeRecuento.Anulado);
        recuento.MotivoDeLaAnulacion.ShouldBe("Se contó dos veces");
        recuento.Numero.ShouldBe(31, "anular no le quita el número: lo gastó al confirmarse (R5)");
    }

    [Fact]
    public void Anular_un_descartado_no_se_puede()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        recuento.Descartar("Se contó el almacén equivocado", Descartado(recuento));

        Should.Throw<InvalidOperationException>(
            () => recuento.Anular("Me equivoqué", null, Anulado(recuento)));
    }

    [Fact]
    public void Anular_dos_veces_no_se_puede()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        _ = ContarYConfirmar(recuento, TodasA(recuento, 5m), _ => 5m);
        recuento.Anular("Se contó dos veces", null, Anulado(recuento));

        Should.Throw<InvalidOperationException>(
            () => recuento.Anular("Otra vez", null, Anulado(recuento)));
    }

    [Fact]
    public void Fuera_de_curso_no_se_cuenta_ni_se_anade_ni_se_quita_nada()
    {
        Recuento confirmado = Abrir(Precargada(Estanteria, Tornillos));
        _ = ContarYConfirmar(confirmado, TodasA(confirmado, 5m), _ => 5m);

        Recuento descartado = Abrir(Precargada(Estanteria, Tornillos));
        descartado.Descartar("Se contó el almacén equivocado", Descartado(descartado));

        foreach (Recuento recuento in new[] { confirmado, descartado })
        {
            Guid linea = recuento.Lineas[0].Id;

            Should.Throw<InvalidOperationException>(() => recuento.Contar(linea, 4m, 5m));
            Should.Throw<InvalidOperationException>(() => recuento.QuitarLinea(linea));
            Should.Throw<InvalidOperationException>(() => recuento.AnadirLinea(
                ClaveDelRecuento.De(OtraEstanteria, Tuercas), Unidades, costeUnitario: null, Momento));
        }
    }

    [Fact]
    public void Abrir_sin_motivo_no_se_puede_porque_es_el_que_lleva_el_ajuste()
    {
        Should.Throw<ArgumentException>(() => Recuento.Abrir(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Apertura, "  ", "EUR", [], Momento));

        Should.Throw<ArgumentException>(() => Recuento.Abrir(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Apertura, new string('x', Recuento.LargoDelMotivo + 1), "EUR", [], Momento));

        Recuento.LargoDelMotivo.ShouldBe(
            Ajuste.LargoDelMotivo, "el motivo del recuento es el de su ajuste, y tiene que caber en él");
    }

    [Fact]
    public void Abrir_sin_alguna_de_sus_dos_series_no_se_puede()
    {
        Should.Throw<ArgumentException>(() => Recuento.Abrir(
            Guid.CreateVersion7(), Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7(),
            Apertura, Motivo, "EUR", [], Momento));

        Should.Throw<ArgumentException>(() => Recuento.Abrir(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.Empty, Guid.CreateVersion7(),
            Apertura, Motivo, "EUR", [], Momento));
    }
}
