using System.Globalization;
using Bastion.Organizacion.Domain.Ejercicios;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Ejercicios;

public sealed class EjercicioTests
{
    private static readonly CultureInfo s_invariante = CultureInfo.InvariantCulture;

    private static readonly Guid s_empresa = Guid.Parse("2f6d5f4e-0000-4000-8000-000000000001");
    private static readonly DateTimeOffset s_momento = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    private static Ejercicio Nuevo(int anio = 2026) => Ejercicio.Crear(
        s_empresa, anio, new DateOnly(anio, 1, 1), new DateOnly(anio, 12, 31), s_momento);

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
            Guid.Empty, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), s_momento));
    }

    [Fact]
    public void La_fecha_de_fin_no_puede_ser_anterior_a_la_de_inicio()
    {
        Should.Throw<ArgumentException>(() => Ejercicio.Crear(
            s_empresa, 2026, new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 1), s_momento));
    }

    [Fact]
    public void Un_ejercicio_puede_no_coincidir_con_el_ano_natural()
    {
        // El art. 26 de la Ley del Impuesto sobre Sociedades permite un ejercicio partido.
        // Rechazarlo por "no cuadra con el año" dejaría fuera a empresas perfectamente legales.
        var partido = Ejercicio.Crear(
            s_empresa, 2026, new DateOnly(2026, 7, 1), new DateOnly(2027, 6, 30), s_momento);

        partido.FechaDeFin.ShouldBe(new DateOnly(2027, 6, 30));
    }

    [Fact]
    public void Un_ejercicio_no_puede_durar_mas_de_doce_meses()
    {
        Should.Throw<ArgumentException>(() => Ejercicio.Crear(
            s_empresa, 2026, new DateOnly(2026, 1, 1), new DateOnly(2027, 6, 30), s_momento));
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
    public void Cerrar_dos_veces_SI_es_un_error_y_dejo_de_ser_idempotente_en_el_2_6()
    {
        // Hasta el ítem 2.6 cerrar lo cerrado no cambiaba nada y se dejaba pasar: cerrar era
        // asignar un estado, y ahorrarse la comprobación salía barato. Dejó de salir barato
        // cuando cerrar pasó a tener precondiciones —ningún borrador con fecha dentro— y
        // consecuencias —reabrir lleva permiso propio, motivo y evento auditado—. Un segundo
        // cierre que contesta «hecho» esconde el caso que importa: que otro se adelantó.
        Ejercicio ejercicio = Nuevo();
        ejercicio.Cerrar();

        Should.Throw<InvalidOperationException>(ejercicio.Cerrar);
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
    public void Reabrir_lo_que_ya_estaba_abierto_tampoco_pasa_en_silencio()
    {
        // Y aquí pesa más que en cerrar: una reapertura deja evento auditado con su motivo, y
        // una que no reabre nada dejaría en la traza un hecho que no ocurrió.
        Ejercicio ejercicio = Nuevo();

        Should.Throw<InvalidOperationException>(ejercicio.Reabrir);
        ejercicio.Estado.ShouldBe(EstadoDeEjercicio.Abierto);
    }

    /// <summary>
    /// Los seis casos de <c>LoQueDejariaFuera</c>, con el día exacto de cada frontera.
    /// </summary>
    /// <remarks>
    /// <b>Aquí y no en integración a propósito.</b> Lo que decide si un documento se queda fuera
    /// es un <c>AddDays(-1)</c> y un <c>AddDays(+1)</c>, y equivocarse en un día no se ve en un
    /// caso de API: se ve poniendo el documento justo en la frontera, que es un caso por cada
    /// lado. Levantar un contenedor para cada uno es la forma de que estos casos no se escriban.
    /// </remarks>
    /// <param name="inicio">Primer día del intervalo nuevo.</param>
    /// <param name="fin">Último día del intervalo nuevo.</param>
    /// <param name="esperado">Los trozos que tienen que quedar fuera, como <c>díadía→díadía</c>.</param>
    [Theory]
    // El mismo intervalo: no deja nada fuera.
    [InlineData("2026-01-01", "2026-12-31", "")]
    // Más ancho por los dos lados: absorbe días, pero no expulsa ninguno.
    [InlineData("2025-06-01", "2027-06-30", "")]
    // Encoge por delante: se queda fuera enero, y el último día de fuera es la víspera.
    [InlineData("2026-02-01", "2026-12-31", "2026-01-01>2026-01-31")]
    // Encoge por detrás: se queda fuera diciembre, desde el día siguiente al nuevo fin.
    [InlineData("2026-01-01", "2026-11-30", "2026-12-01>2026-12-31")]
    // Encoge por los DOS lados: dos trozos, uno por delante y otro por detrás.
    [InlineData("2026-02-01", "2026-11-30", "2026-01-01>2026-01-31|2026-12-01>2026-12-31")]
    // Se va entero a otro año: deja fuera el intervalo actual COMPLETO, y en un solo trozo.
    [InlineData("2027-01-01", "2027-12-31", "2026-01-01>2026-12-31")]
    public void Lo_que_un_intervalo_nuevo_dejaria_fuera_se_cuenta_por_dias_y_no_por_meses(
        string inicio, string fin, string esperado)
    {
        Ejercicio ejercicio = Nuevo();

        IReadOnlyList<(DateOnly Desde, DateOnly Hasta)> fuera =
            ejercicio.LoQueDejariaFuera(DateOnly.Parse(inicio, s_invariante), DateOnly.Parse(fin, s_invariante));

        string.Join(
            "|",
            fuera.Select(tramo => tramo.Desde.ToString("yyyy-MM-dd", s_invariante) + ">" +
                tramo.Hasta.ToString("yyyy-MM-dd", s_invariante)))
            .ShouldBe(esperado);
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
