using Bastion.Organizacion.Domain.Ejercicios;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Ejercicios;

public sealed class EjercicioTests
{
    private static readonly Guid s_empresa = Guid.Parse("2f6d5f4e-0000-4000-8000-000000000001");

    private static Ejercicio Nuevo(int anio = 2026) => Ejercicio.Crear(
        s_empresa, anio, new DateOnly(anio, 1, 1), new DateOnly(anio, 12, 31));

    [Fact]
    public void Un_ejercicio_nace_abierto_y_con_su_empresa()
    {
        Ejercicio ejercicio = Nuevo();

        ejercicio.Estado.ShouldBe(EstadoDeEjercicio.Abierto);
        ejercicio.EmpresaId.ShouldBe(s_empresa);
        ejercicio.Anio.ShouldBe(2026);
    }

    [Fact]
    public void Las_fechas_de_un_ejercicio_son_fechas_de_calendario_sin_hora_ni_zona()
    {
        // R14 y el sentido común contable: el ejercicio 2026 empieza el 1 de enero de 2026 en
        // Madrid y en Canarias. Un `timestamptz` obligaría a elegir una zona horaria para algo
        // que no la tiene, y el 1 de enero a las 00:00 en Madrid es el 31 de diciembre en UTC-1.
        Ejercicio ejercicio = Nuevo();

        ejercicio.FechaDeInicio.ShouldBe(new DateOnly(2026, 1, 1));
        ejercicio.FechaDeFin.ShouldBe(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void Un_ejercicio_sin_empresa_no_existe()
    {
        // R8: `empresa_id` en toda entidad transaccional, desde la primera tabla.
        Should.Throw<ArgumentException>(() => Ejercicio.Crear(
            Guid.Empty, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void La_fecha_de_fin_no_puede_ser_anterior_a_la_de_inicio()
    {
        Should.Throw<ArgumentException>(() => Ejercicio.Crear(
            s_empresa, 2026, new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Un_ejercicio_puede_no_coincidir_con_el_ano_natural()
    {
        // El art. 26 de la Ley del Impuesto sobre Sociedades permite un ejercicio partido.
        // Rechazarlo por "no cuadra con el año" dejaría fuera a empresas perfectamente legales.
        var partido = Ejercicio.Crear(
            s_empresa, 2026, new DateOnly(2026, 7, 1), new DateOnly(2027, 6, 30));

        partido.FechaDeFin.ShouldBe(new DateOnly(2027, 6, 30));
    }

    [Fact]
    public void Un_ejercicio_no_puede_durar_mas_de_doce_meses()
    {
        Should.Throw<ArgumentException>(() => Ejercicio.Crear(
            s_empresa, 2026, new DateOnly(2026, 1, 1), new DateOnly(2027, 6, 30)));
    }

    [Fact]
    public void Cerrar_un_ejercicio_lo_deja_cerrado()
    {
        Ejercicio ejercicio = Nuevo();

        ejercicio.Cerrar();

        ejercicio.Estado.ShouldBe(EstadoDeEjercicio.Cerrado);
    }

    [Fact]
    public void Un_ejercicio_cerrado_no_admite_cambios_de_fechas()
    {
        // R9: no se registra nada en un ejercicio cerrado, y mover sus fechas movería las
        // operaciones que caen dentro.
        Ejercicio ejercicio = Nuevo();
        ejercicio.Cerrar();

        Should.Throw<InvalidOperationException>(() =>
            ejercicio.Modificar(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)));
    }

    [Fact]
    public void Cerrar_dos_veces_no_es_un_error_de_programa()
    {
        // Cerrar lo ya cerrado no cambia nada: la operación es idempotente por diseño, que es
        // más barato que obligar a comprobar el estado antes de cada llamada.
        Ejercicio ejercicio = Nuevo();
        ejercicio.Cerrar();

        Should.NotThrow(ejercicio.Cerrar);
        ejercicio.Estado.ShouldBe(EstadoDeEjercicio.Cerrado);
    }

    [Fact]
    public void Reabrir_un_ejercicio_es_posible_y_es_una_operacion_con_nombre()
    {
        Ejercicio ejercicio = Nuevo();
        ejercicio.Cerrar();

        ejercicio.Reabrir();

        ejercicio.Estado.ShouldBe(EstadoDeEjercicio.Abierto);
    }

    [Fact]
    public void Un_ejercicio_no_lleva_estado_de_bloqueo()
    {
        // Decisión escrita a propósito: `Bloqueado` (R16) es el estado que exige el art. 32 de
        // la LOPDGDD para DATOS PERSONALES. Un ejercicio contable no tiene ninguno: es un
        // intervalo de fechas. Su ciclo de vida es el de R9 —abierto y cerrado— y mezclar las
        // dos máquinas de estados haría que "cerrar el ejercicio" y "bloquear por derecho de
        // supresión" compartieran columna, que es justo lo que no se quiere.
        typeof(EstadoDeEjercicio).GetEnumNames()
            .ShouldBe([nameof(EstadoDeEjercicio.Abierto), nameof(EstadoDeEjercicio.Cerrado)]);
    }
}
