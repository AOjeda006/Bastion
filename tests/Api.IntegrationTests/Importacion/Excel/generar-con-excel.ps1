# Genera con Excel de verdad los dos CSV de terceros contra los que se comprueba la importación.
#
# No corre en la CI —allí no hay Excel— ni hace falta que corra: los ficheros que escribe se
# commitean TAL CUAL (ver `.gitattributes`), y este guion queda para decir de dónde salieron y
# poder repetirlo. Lo que importa de él son tres cosas que un fichero escrito a mano no tendría:
#
#   - `Local` a verdadero en `SaveAs`, que es lo que hace «Guardar como» desde la interfaz: sin
#     él, Excel escribe con la configuración de EE. UU. —comas y punto decimal— aunque la máquina
#     esté en español, y la fixture probaría un dialecto que ningún usuario produce.
#   - Celdas de verdad: un importe numérico con formato de miles, un booleano, un salto de línea
#     dentro de una celda, un punto y coma y unas comillas dentro de un texto, y una fila en blanco
#     en medio del rango usado. Cómo los escribe es lo que se comprueba, no cómo creemos que lo hace.
#   - Los identificadores fiscales son marcadores `{{NIF:n}}` que el test sustituye por NIF
#     inventados: ni aquí ni en el fichero va el identificador de nadie.
#
# Uso, en una máquina con Excel y la configuración regional de España:
#   powershell -NoProfile -ExecutionPolicy Bypass -File generar-con-excel.ps1

$ErrorActionPreference = 'Stop'

$destino = $PSScriptRoot
$faltante = [Type]::Missing
$xlCsv = 6        # «CSV (delimitado por comas)»
$xlCsvUtf8 = 62   # «CSV UTF-8 (delimitado por comas)»

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

try {
    $libro = $excel.Workbooks.Add()
    $hoja = $libro.Worksheets.Item(1)
    $hoja.Cells.NumberFormat = '@'

    $filas = @(
        @('identificacion_pais', 'identificacion_numero', 'razon_social', 'nombre_comercial',
          'domicilio_calle', 'domicilio_numero', 'domicilio_codigo_postal', 'domicilio_poblacion',
          'domicilio_subdivision', 'domicilio_pais', 'es_cliente', 'es_proveedor', 'territorio',
          'recargo_de_equivalencia', 'criterio_de_caja', 'sujeto_a_retencion_irpf', 'limite_credito',
          'limite_credito_divisa'),
        @('ES', '{{NIF:1}}', 'Ferretería Peña, S.L.', 'Peña', 'Calle de la Constitución', '12', '08001',
          'Barcelona', 'Barcelona', 'ES', 'sí', 'no', '', 'no', 'no', 'no', $null, 'EUR'),
        @('ES', '{{NIF:2}}', 'Distribuciones Norte; Sur, S.A.', "Almacén`ncentral", 'Avenida Marítima', '',
          '35001', 'Las Palmas de Gran Canaria', 'Las Palmas', 'ES', $null, $null, 'Canarias', 'no', 'sí',
          'no', $null, 'EUR'),
        @(),
        @('ES', '{{NIF:3}}', 'Bar "El Rincón"', '', 'Plaza Mayor', '1', '28012', 'Madrid', 'Madrid', 'ES',
          'no', 'sí', 'PeninsulaYBaleares', 'no', 'no', 'no', '', '')
    )

    for ($f = 0; $f -lt $filas.Count; $f++) {
        for ($c = 0; $c -lt $filas[$f].Count; $c++) {
            if ($null -ne $filas[$f][$c]) {
                $hoja.Cells.Item($f + 1, $c + 1).Value2 = $filas[$f][$c]
            }
        }
    }

    # Un importe numérico con formato de miles: Excel lo escribe como lo enseña, «1.234,56».
    $importe = $hoja.Range('Q2')
    $importe.NumberFormatLocal = '#.##0,00'
    $importe.Value2 = 1234.56

    # Un importe sin formato: «2500», sin miles ni decimales.
    $otro = $hoja.Range('Q3')
    $otro.NumberFormatLocal = 'Estándar'
    $otro.Value2 = 2500

    # Dos booleanos de verdad, que en español Excel escribe «VERDADERO».
    foreach ($celda in 'K3', 'L3') {
        $booleano = $hoja.Range($celda)
        $booleano.NumberFormatLocal = 'Estándar'
        $booleano.Value2 = $true
    }

    $libro.SaveAs((Join-Path $destino 'terceros-csv-delimitado-por-comas.csv'), $xlCsv,
        $faltante, $faltante, $faltante, $faltante, $faltante, $faltante, $faltante, $faltante,
        $faltante, $true)
    $libro.SaveAs((Join-Path $destino 'terceros-csv-utf8.csv'), $xlCsvUtf8,
        $faltante, $faltante, $faltante, $faltante, $faltante, $faltante, $faltante, $faltante,
        $faltante, $true)
    $libro.Close($false)
}
finally {
    $excel.Quit()
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
}
