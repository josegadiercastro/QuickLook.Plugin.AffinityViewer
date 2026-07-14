using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using QuickLook.Common.Plugin;

namespace QuickLook.Plugin.AffinityViewer;

public partial class AffinityPanel : UserControl, IDisposable
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 8.0;

    private Point _panStart;
    private Point _scrollStart;
    private bool _isPanning;

    public AffinityPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Carga el archivo Affinity indicado, probando en orden:
    /// 1) Miniatura del Shell de Windows (requiere Affinity instalado)
    /// 2) Vista previa JPEG/PNG incrustada en el propio archivo
    /// 3) Tarjeta informativa si ninguna de las anteriores funcionó
    /// </summary>
    public void LoadAffinityFile(string path, ContextObject context)
    {
        var kind = DescribeDocumentKind(path);
        var fileInfo = new FileInfo(path);

        BitmapSource preview = ShellThumbnailProvider.TryGetThumbnail(path);
        var source = "Miniatura de Windows Shell";

        if (preview == null)
        {
            preview = EmbeddedPreviewExtractor.TryExtractLargestPreview(path);
            source = "Vista previa incrustada en el archivo";
        }

        if (preview != null)
        {
            PreviewImage.Source = preview;
            PreviewImage.Visibility = Visibility.Visible;
            FallbackPanel.Visibility = Visibility.Collapsed;

            SourceBadge.Text = source;

            // Ajusta el tamaño preferido de la ventana a la relación de aspecto real
            var size = FitWithinBounds(preview.PixelWidth, preview.PixelHeight, 1400, 1000);
            context.SetPreferredSizeFit(size, 0.85d);
        }
        else
        {
            PreviewImage.Visibility = Visibility.Collapsed;
            FallbackPanel.Visibility = Visibility.Visible;
            SourceBadge.Text = string.Empty;

            FallbackTitle.Text = "Sin vista previa disponible";
            FallbackDetail.Text =
                $"No se pudo generar una miniatura para este {kind}. " +
                "Instala Affinity Photo, Designer o Publisher para que Windows " +
                "pueda generar miniaturas de estos archivos, o ábrelo directamente " +
                "con la aplicación correspondiente.";
        }

        InfoText.Text = $"{kind} · {FormatSize(fileInfo.Length)} · Modificado: {fileInfo.LastWriteTime:g}";
    }

    public void ShowError(string message)
    {
        PreviewImage.Visibility = Visibility.Collapsed;
        FallbackPanel.Visibility = Visibility.Visible;
        FallbackIcon.Text = "⚠️";
        FallbackTitle.Text = "No se pudo previsualizar el archivo";
        FallbackDetail.Text = message;
        InfoText.Text = string.Empty;
    }

    private static string DescribeDocumentKind(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".afphoto" => "Documento de Affinity Photo",
            ".afdesign" => "Documento de Affinity Designer",
            ".afpub" => "Documento de Affinity Publisher",
            ".afassets" => "Biblioteca de assets de Affinity",
            ".afbrushes" => "Pinceles de Affinity",
            ".afpalette" => "Paleta de Affinity",
            _ => "Documento de Affinity"
        };
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.#} {units[unitIndex]}";
    }

    private static Size FitWithinBounds(int width, int height, double maxWidth, double maxHeight)
    {
        if (width <= 0 || height <= 0)
            return new Size(maxWidth, maxHeight);

        var scale = Math.Min(maxWidth / width, maxHeight / height);
        scale = Math.Min(scale, 1.0); // no ampliar más allá del tamaño real
        return new Size(Math.Max(width * scale, 400), Math.Max(height * scale, 300));
    }

    // ----- Zoom con rueda del ratón -----

    private void ScrollHost_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control && PreviewImage.Visibility != Visibility.Visible)
            return;

        e.Handled = true;

        var factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
        var newScale = Clamp(ImageScale.ScaleX * factor, MinZoom, MaxZoom);
        ImageScale.ScaleX = newScale;
        ImageScale.ScaleY = newScale;
    }

    // ----- Paneo arrastrando con el botón izquierdo -----

    private void ScrollHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (PreviewImage.Visibility != Visibility.Visible)
            return;

        _isPanning = true;
        _panStart = e.GetPosition(ScrollHost);
        _scrollStart = new Point(ScrollHost.HorizontalOffset, ScrollHost.VerticalOffset);
        ScrollHost.CaptureMouse();
    }

    private void ScrollHost_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning)
            return;

        var current = e.GetPosition(ScrollHost);
        var delta = _panStart - current;

        ScrollHost.ScrollToHorizontalOffset(_scrollStart.X + delta.X);
        ScrollHost.ScrollToVerticalOffset(_scrollStart.Y + delta.Y);
    }

    private void ScrollHost_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isPanning = false;
        ScrollHost.ReleaseMouseCapture();
    }

    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

    public void Dispose()
    {
        PreviewImage.Source = null;
    }
}
