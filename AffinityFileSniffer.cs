using System;
using System.IO;
using System.Text;

namespace QuickLook.Plugin.AffinityViewer;

/// <summary>
/// Comprobaciones ligeras sobre el contenido binario de un archivo Affinity.
/// Los formatos .afphoto/.afdesign/.afpub son contenedores propietarios de
/// Serif/Affinity (no documentados públicamente) organizados en "chunks".
/// No existe una firma mágica oficial y estable entre versiones, así que
/// aquí solo se hacen comprobaciones defensivas y baratas para descartar
/// archivos claramente corruptos o con la extensión equivocada.
/// </summary>
internal static class AffinityFileSniffer
{
    private const int SniffLength = 4096;

    public static bool LooksLikeAffinityFile(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (fs.Length < 16)
                return false;

            var buffer = new byte[Math.Min(SniffLength, fs.Length)];
            var read = fs.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                return false;

            // 1) Nunca debe ser un ZIP/PDF/imagen renombrada: descartamos
            //    firmas de otros formatos frecuentemente confundidos.
            if (StartsWith(buffer, "PK\x03\x04") || // ZIP / Office moderno
                StartsWith(buffer, "%PDF") ||
                StartsWith(buffer, "\x89PNG") ||
                StartsWith(buffer, "\xFF\xD8\xFF"))
                return false;

            // 2) Los archivos Affinity suelen contener, en los primeros KB,
            //    cadenas identificativas del propio formato o del fabricante.
            //    Esta heurística es tolerante a fallos: si no encuentra nada
            //    concluyente, deja pasar el archivo igualmente (la extensión
            //    ya filtró la mayoría de falsos positivos) para no bloquear
            //    la previsualización de documentos legítimos.
            var text = Encoding.ASCII.GetString(buffer);
            if (text.IndexOf("Serif", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Affinity", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return true;
        }
        catch
        {
            // Si no se puede leer (permisos, archivo bloqueado, etc.) dejamos
            // que CanHandle lo intente igualmente; View() reportará el error.
            return true;
        }
    }

    private static bool StartsWith(byte[] buffer, string asciiMagic)
    {
        if (buffer.Length < asciiMagic.Length)
            return false;

        for (var i = 0; i < asciiMagic.Length; i++)
        {
            if (buffer[i] != (byte)asciiMagic[i])
                return false;
        }

        return true;
    }
}
