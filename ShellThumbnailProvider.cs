using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickLook.Plugin.AffinityViewer;

/// <summary>
/// Pide a Windows Shell la miniatura de un archivo usando IShellItemImageFactory.
/// Esto funciona para .afphoto/.afdesign/.afpub siempre que Affinity Photo,
/// Designer o Publisher (o cualquier otro manejador de miniaturas registrado
/// para esas extensiones) esté instalado, ya que es exactamente el mismo
/// mecanismo que usa el Explorador de Windows para mostrar las miniaturas
/// en las carpetas.
/// </summary>
internal static class ShellThumbnailProvider
{
    private const int SIIGBF_RESIZETOFIT = 0x00;
    private const int SIIGBF_BIGGERSIZEOK = 0x01;
    private const int SIIGBF_THUMBNAILONLY = 0x08; // fuerza miniatura real, no un icono genérico

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(
            [In, MarshalAs(UnmanagedType.Struct)] SIZE size,
            [In] int flags,
            [Out] out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;

        public SIZE(int cx, int cy)
        {
            this.cx = cx;
            this.cy = cy;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    private static readonly Guid IID_IShellItemImageFactory = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

    /// <summary>
    /// Intenta obtener una miniatura de hasta <paramref name="maxSize"/> px de lado.
    /// Devuelve null si el shell no puede generar una (p. ej. no hay ningún
    /// manejador de miniaturas registrado para la extensión).
    /// </summary>
    public static BitmapSource TryGetThumbnail(string path, int maxSize = 1600)
    {
        object shellItem = null;
        var hbitmap = IntPtr.Zero;

        try
        {
            var riid = IID_IShellItemImageFactory;
            var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref riid, out shellItem);
            if (hr != 0 || shellItem is not IShellItemImageFactory factory)
                return null;

            var flags = SIIGBF_THUMBNAILONLY | SIIGBF_BIGGERSIZEOK;
            hr = factory.GetImage(new SIZE(maxSize, maxSize), flags, out hbitmap);

            if (hr != 0 || hbitmap == IntPtr.Zero)
            {
                // Reintenta permitiendo que el shell devuelva un icono grande
                // si no existe una miniatura real cacheada.
                flags = SIIGBF_RESIZETOFIT | SIIGBF_BIGGERSIZEOK;
                hr = factory.GetImage(new SIZE(maxSize, maxSize), flags, out hbitmap);
                if (hr != 0 || hbitmap == IntPtr.Zero)
                    return null;
            }

            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                hbitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hbitmap != IntPtr.Zero)
                DeleteObject(hbitmap);

            if (shellItem != null)
                Marshal.ReleaseComObject(shellItem);
        }
    }
}
