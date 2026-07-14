using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace QuickLook.Plugin.AffinityViewer;

/// <summary>
/// Último recurso cuando el shell de Windows no puede generar una miniatura
/// (p. ej. no hay ninguna app de Affinity instalada en la máquina). Muchos
/// documentos Affinity incluyen, junto a los datos del proyecto, una vista
/// previa en JPEG o PNG (usada por Affinity para su propio panel "Recientes"
/// y para las miniaturas de Explorer/Finder). Como el formato no es público,
/// no podemos leer un índice de chunks fiable, así que se busca directamente
/// por firma de bytes y se decodifica el candidato más grande y válido.
/// Esto es una heurística: puede no encontrar nada en documentos guardados
/// sin vista previa incrustada, o en versiones futuras del formato.
/// </summary>
internal static class EmbeddedPreviewExtractor
{
    private static readonly byte[] JpegStart = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] JpegEnd = { 0xFF, 0xD9 };
    private static readonly byte[] PngStart = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] PngEnd = { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 }; // "IEND" + CRC

    // Límite de seguridad para no cargar archivos gigantes en memoria.
    private const long MaxFileBytesToScan = 200 * 1024 * 1024;

    public static BitmapSource TryExtractLargestPreview(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length == 0 || info.Length > MaxFileBytesToScan)
            return null;

        byte[] data;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            data = new byte[fs.Length];
            var offset = 0;
            int read;
            while (offset < data.Length && (read = fs.Read(data, offset, data.Length - offset)) > 0)
                offset += read;
        }

        var candidates = new List<(int start, int length)>();
        candidates.AddRange(FindBlocks(data, JpegStart, JpegEnd, includeEnd: true));
        candidates.AddRange(FindBlocks(data, PngStart, PngEnd, includeEnd: true));

        if (candidates.Count == 0)
            return null;

        // Nos quedamos con el bloque más grande: normalmente es la vista
        // previa de mayor resolución, y descarta iconos/miniaturas internas
        // más pequeñas que a veces también van incrustadas.
        candidates.Sort((a, b) => b.length.CompareTo(a.length));

        foreach (var (start, length) in candidates)
        {
            var bmp = TryDecode(data, start, length);
            if (bmp != null)
                return bmp;
        }

        return null;
    }

    private static IEnumerable<(int start, int length)> FindBlocks(byte[] data, byte[] startMagic, byte[] endMagic, bool includeEnd)
    {
        var results = new List<(int, int)>();
        var searchFrom = 0;

        while (true)
        {
            var start = IndexOf(data, startMagic, searchFrom);
            if (start < 0)
                break;

            var end = IndexOf(data, endMagic, start + startMagic.Length);
            if (end < 0)
                break;

            var blockEnd = includeEnd ? end + endMagic.Length : end;
            var length = blockEnd - start;

            // Descarta bloques irrisoriamente pequeños (ruido, no una imagen real)
            if (length > 512)
                results.Add((start, length));

            searchFrom = blockEnd;
        }

        return results;
    }

    private static BitmapSource TryDecode(byte[] data, int start, int length)
    {
        try
        {
            using var ms = new MemoryStream(data, start, length, writable: false);
            var decoder = BitmapDecoder.Create(
                ms,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch
        {
            // Firma coincidente pero datos corruptos/parciales: se descarta
            // y el llamador prueba el siguiente candidato.
            return null;
        }
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int startIndex)
    {
        if (startIndex < 0 || startIndex >= haystack.Length)
            return -1;

        var limit = haystack.Length - needle.Length;
        for (var i = startIndex; i <= limit; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return i;
        }

        return -1;
    }
}
