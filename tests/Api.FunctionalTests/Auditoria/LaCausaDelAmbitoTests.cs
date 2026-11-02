using System.Diagnostics;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Auditoria;

/// <summary>
/// La causa de lo que se escribe en un ámbito, en una petición: quién, con qué correlación y en
/// qué traza (3.4, hueco 3).
/// </summary>
/// <remarks>
/// Lo que pasa detrás del despachador, con la causa de la fila, lo prueba
/// <c>ElManejadorVaConQuienLoCausoTests</c> contra la base: abrir ese ámbito es de la bandeja y no
/// se alcanza desde aquí. Esto es la aritmética, sin base y sin host.
/// </remarks>
public sealed class LaCausaDelAmbitoTests
{
    [Fact]
    public void La_correlacion_es_una_por_ambito_y_no_una_por_lectura()
    {
        // Hasta el 3.4 era una por `SaveChanges`. Ahora todo lo que escribe una operación se junta
        // por ella, y la fila de la bandeja lleva la misma que la traza de auditoría.
        CausaDelAmbito causa = new(new Usuario(null));
        Guid primera = causa.CorrelacionId;

        primera.ShouldNotBe(Guid.Empty);
        causa.CorrelacionId.ShouldBe(primera, "leerla otra vez en el mismo ámbito no la cambia");
        causa.Actual.CorrelacionId.ShouldBe(primera);

        new CausaDelAmbito(new Usuario(null)).CorrelacionId.ShouldNotBe(primera, "dos ámbitos, dos operaciones");
    }

    [Fact]
    public void El_usuario_es_el_de_la_sesion_y_sin_sesion_no_es_nadie()
    {
        var usuarioId = Guid.CreateVersion7();

        new CausaDelAmbito(new Usuario(usuarioId)).Actual.UsuarioId.ShouldBe(usuarioId);

        // Sin sesión, el puerto lanza si se le pide el usuario: la causa no se lo pide.
        new CausaDelAmbito(new Usuario(null)).Actual.UsuarioId.ShouldBeNull();
    }

    [Fact]
    public void La_traza_es_la_de_la_actividad_en_curso_si_es_un_traceparent()
    {
        // El caso fija su ambiente: la actividad en curso es del flujo asíncrono, y la del runner,
        // si la hubiera, no es la de este caso.
        Activity.Current = null;

        CausaDelAmbito causa = new(new Usuario(null));

        causa.Actual.ContextoDeTraza.ShouldBeNull("sin actividad en curso no hay traza");

        using (Activity w3c = new Activity("w3c").SetIdFormat(ActivityIdFormat.W3C).Start())
        {
            causa.Actual.ContextoDeTraza.ShouldBe(w3c.Id);

            // La columna mide lo que mide el formato de verdad, no un número tecleado a ojo.
            w3c.Id.ShouldNotBeNull().Length.ShouldBe(ConfiguracionDeLaBandeja.LongitudDelContextoDeTraza);
        }

        using (Activity jerarquica = new Activity("jerarquica").SetIdFormat(ActivityIdFormat.Hierarchical).Start())
        {
            // La pareja: hay actividad, y tiene identificador. Lo que no tiene es un `traceparent`,
            // y del que no lo es no puede colgar nadie.
            jerarquica.Id.ShouldNotBeNull();
            causa.Actual.ContextoDeTraza.ShouldBeNull("un identificador jerárquico no es un traceparent");
        }
    }

    private sealed class Usuario(Guid? usuarioId) : IUsuarioActual
    {
        public bool EstaAutenticado => usuarioId is not null;

        public Guid UsuarioId => usuarioId ?? throw new InvalidOperationException("No hay sesión.");

        public Guid EmpresaId => throw new NotSupportedException("La causa no lee la empresa.");

        public bool Tiene(Permiso permiso) => false;
    }
}
