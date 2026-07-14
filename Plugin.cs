// Copyright © 2026
//
// QuickLook.Plugin.AffinityViewer
// Plugin de QuickLook para previsualizar archivos de Affinity
// (.afphoto, .afdesign, .afpub) creados por Affinity Photo, Designer y Publisher.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.IO;
using System.Linq;
using System.Windows;
using QuickLook.Common.Plugin;

namespace QuickLook.Plugin.AffinityViewer;

public class Plugin : IViewer
{
    // Extensiones de la suite Affinity (v1 y v2 comparten las mismas extensiones)
    private static readonly string[] Extensions =
    {
        ".afphoto",     // Affinity Photo
        ".afdesign",    // Affinity Designer
        ".afpub",       // Affinity Publisher
        ".afassets",    // Bibliotecas de assets de Affinity
        ".afbrushes",   // Pinceles de Affinity
        ".afpalette"    // Paletas de Affinity
    };

    // Cabeceras binarias conocidas de los formatos Affinity, usadas como
    // verificación adicional de contenido (ver AffinityFileSniffer.cs).
    private AffinityPanel _panel;

    /// <summary>
    /// Prioridad neutra: no compite con visores más específicos si alguno los soporta.
    /// </summary>
    public int Priority => 0;

    public void Init()
    {
        // Nada que precargar; toda la extracción es perezosa y ocurre en View().
    }

    public bool CanHandle(string path)
    {
        if (string.IsNullOrEmpty(path) || Directory.Exists(path))
            return false;

        var ext = Path.GetExtension(path);
        if (!Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return false;

        // Verificación ligera de cabecera para evitar falsos positivos
        // (archivo con extensión .afphoto pero contenido corrupto/ajeno).
        return AffinityFileSniffer.LooksLikeAffinityFile(path);
    }

    public void Prepare(string path, ContextObject context)
    {
        // Tamaño de ventana preferido; se ajustará según la miniatura real en View().
        context.SetPreferredSizeFit(new Size(1200, 900), 0.8d);
    }

    public void View(string path, ContextObject context)
    {
        _panel = new AffinityPanel();
        context.ViewerContent = _panel;
        context.Title = Path.GetFileName(path);

        _panel.Loaded += (_, _) =>
        {
            _panel.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    _panel.LoadAffinityFile(path, context);
                }
                catch (Exception ex)
                {
                    _panel.ShowError(ex.Message);
                }
                finally
                {
                    context.IsBusy = false;
                }
            });
        };
    }

    public void Cleanup()
    {
        _panel?.Dispose();
        _panel = null;
    }
}
