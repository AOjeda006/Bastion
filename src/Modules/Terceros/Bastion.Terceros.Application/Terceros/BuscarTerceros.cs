using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Terceros.Application.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Busca terceros por un criterio que no puede viajar en la URL (ADR-0025).</summary>
/// <remarks>
/// <para>
/// <b>Es el caso peligroso de verdad del ADR-0025</b>, más que el de empresas: el NIF de una
/// sociedad es un dato de registro público, y el de un cliente es, muy a menudo, el DNI de una
/// persona física. Y además es la búsqueda que alguien va a hacer todos los días, así que por la
/// cadena de consulta quedaría escrita todos los días —historial, enlace copiado, cabecera
/// <c>Referer</c>, registro de acceso del servidor de delante—.
/// </para>
/// <para>
/// <b>Devuelve <c>Resultado</c> y el listado no</b>, por lo mismo que en empresas: aquí hay
/// entradas que pueden estar mal de una manera que el borde no sabe comprobar —un NIF cuyo
/// carácter de control no cuadra, un cursor ilegible, una búsqueda sin ningún criterio— y cada una
/// tiene una respuesta distinta que dar (ADR-0004).
/// </para>
/// </remarks>
public interface IBuscarTerceros
{
    /// <summary>Ejecuta la búsqueda.</summary>
    /// <param name="peticion">Criterio y por dónde seguir, tal como llegaron en el cuerpo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TramoDe<TerceroDto>>> EjecutarAsync(
        BuscarTercerosDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IBuscarTerceros"/>
internal sealed class BuscarTerceros(IRepositorioDeTerceros terceros) : IBuscarTerceros
{
    public async Task<Resultado<TramoDe<TerceroDto>>> EjecutarAsync(
        BuscarTercerosDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var errores = new ErroresPorCampo();
        IdentificacionFiscal? identificacion = null;

        if (!string.IsNullOrWhiteSpace(peticion.Numero))
        {
            // El país por omisión es España, que es la búsqueda de todos los días. Lo que NO hay
            // es «en cualquier país»: el mismo número puede identificar a dos personas distintas
            // en dos países, y mezclarlos enseñaría la ficha de alguien a quien no se buscaba.
            //
            // Se lee POR EL MISMO CAMINO que el alta, y eso es lo que hace que buscar
            // «B-1234 5678» encuentre lo que se dio de alta como «b12345678». Con dos lecturas
            // distintas, la ficha existiría y no aparecería.
            identificacion = Identificaciones.Leer(
                peticion.Pais ?? IdentificacionFiscal.PaisDeEspana,
                peticion.Numero,
                string.Empty,
                errores);
        }
        else if (!string.IsNullOrWhiteSpace(peticion.Pais)
                 && IdentificacionFiscal.PaisNormalizado(peticion.Pais) is null)
        {
            // Un país sin número no filtra nada por sí solo —no se busca «todos los franceses»—,
            // pero si viene escrito y no es un país, callarse sería contestar un tramo vacío a una
            // petición que estaba mal.
            errores.Agregar(
                "pais",
                "No es un código de país: se escribe con las dos letras de ISO 3166-1 alfa-2, " +
                "como ES, FR o PT.");
        }

        string? nombre = string.IsNullOrWhiteSpace(peticion.Nombre)
            ? null
            : peticion.Nombre.Trim();

        // Una búsqueda sin criterio es el listado entero pedido por un camino que no pagina por
        // número. No se rechaza por purismo: el listado YA existe, con su tope y su orden, y tener
        // dos formas de pedir lo mismo garantiza que una de las dos envejezca sin que nadie la
        // mire.
        if (identificacion is null && nombre is null && !errores.Hay)
        {
            errores.Agregar(
                "numero",
                "Indique al menos un criterio. Para ver todos los terceros está el listado " +
                "GET /api/v1/terceros/terceros.");
        }

        Guid? desde = null;

        if (peticion.Cursor is not null)
        {
            if (Cursores.Intentar(peticion.Cursor, out Guid posicion))
            {
                desde = posicion;
            }
            else
            {
                errores.Agregar(
                    "cursor",
                    "No se entiende. Devuelva el cursor tal como vino en el tramo anterior, sin " +
                    "componerlo a mano.");
            }
        }

        if (errores.Hay)
        {
            return Resultado.Fallo<TramoDe<TerceroDto>>(errores.AError());
        }

        // Sin ámbito de bloqueo, y es lo importante de esta línea: una búsqueda por identificador
        // fiscal que viera lo bloqueado sería la puerta trasera del art. 32 con el criterio más
        // cómodo posible. Lo bloqueado se lista por su camino, con su permiso y su traza.
        TramoDe<Tercero> tramo = await terceros
            .BuscarAsync(
                new CriterioDeTerceros(identificacion?.Pais, identificacion?.Numero, nombre),
                desde,
                peticion.Tamanio,
                cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(
            new TramoDe<TerceroDto>(
                [.. tramo.Elementos.Select(tercero => tercero.ADto())],
                tramo.Tamanio,
                tramo.CursorSiguiente));
    }
}
