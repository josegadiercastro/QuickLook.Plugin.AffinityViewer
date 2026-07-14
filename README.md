# QuickLook.Plugin.AffinityViewer

Plugin para [QuickLook](https://github.com/QL-Win/QuickLook) que añade previsualización de
archivos de la suite **Affinity** (Serif / Canva):

- `.afphoto` — Affinity Photo
- `.afdesign` — Affinity Designer
- `.afpub` — Affinity Publisher
- `.afassets`, `.afbrushes`, `.afpalette` — bibliotecas auxiliares

## ⚠️ Cómo funciona (y sus límites)

Los formatos de Affinity son **contenedores binarios propietarios sin
especificación pública**. No existe una librería libre para decodificar el
lienzo/capas, así que este plugin **no renderiza el documento como lo haría
Affinity**. En su lugar usa, en este orden:

1. **Miniatura de Windows Shell** (`IShellItemImageFactory`): el mismo
   mecanismo que usa el Explorador de Windows para mostrar la miniatura del
   archivo en una carpeta. Funciona **si tienes instalado Affinity Photo,
   Designer o Publisher** (son ellos quienes registran el manejador de
   miniaturas para estas extensiones), en alta resolución.
2. **Vista previa incrustada**: si el shell no puede generar nada (p. ej. no
   tienes Affinity instalado en esa máquina), el plugin busca dentro del
   propio archivo un JPEG/PNG incrustado —muchos documentos Affinity guardan
   una vista previa de este tipo— y muestra el más grande que encuentre.
3. Si ninguna de las dos funciona, se muestra una tarjeta con el tipo de
   documento, tamaño y fecha de modificación, en vez de dejar la ventana en
   blanco.

En la práctica: **con Affinity instalado, la previsualización es fiel y de
alta resolución (opción 1)**. Sin Affinity instalado, depende de si el
archivo concreto guarda una vista previa incrustada.

## 🤖 Compilar automáticamente con GitHub Actions (recomendado, no necesitas Windows)

Este repo ya incluye `.github/workflows/build.yml`, listo para compilar el
plugin en un runner Windows gratuito de GitHub y darte el `.qlplugin` sin
que tengas que instalar nada en tu máquina.

**Pasos:**

1. Crea un repositorio nuevo en GitHub (puede ser privado o público).
2. Sube el contenido de esta carpeta tal cual (incluyendo la carpeta oculta
   `.github/`):
   ```bash
   git init
   git add .
   git commit -m "Affinity Viewer plugin for QuickLook"
   git branch -M main
   git remote add origin https://github.com/TU-USUARIO/TU-REPO.git
   git push -u origin main
   ```
3. Ve a la pestaña **Actions** de tu repo en GitHub. El workflow
   "Build QuickLook.Plugin.AffinityViewer" arranca solo al hacer push (tarda
   ~1-2 minutos: clona QuickLook, compila con .NET 8 en Windows y empaqueta
   el `.qlplugin`).
4. Cuando termine (✅ verde), entra en esa ejecución del workflow y baja
   hasta **Artifacts**: ahí está `QuickLook.Plugin.AffinityViewer` para
   descargar como `.zip` (dentro va el `.qlplugin` ya listo).
5. Descomprime ese zip, coge el archivo `QuickLook.Plugin.AffinityViewer.qlplugin`
   y sigue la sección **🔌 Instalar** de abajo.

Si quieres un enlace de descarga permanente en vez de un artifact temporal,
crea un **Release** en GitHub (pestaña *Releases* → *Draft a new release* →
*Publish*): el workflow se relanza automáticamente y adjunta el `.qlplugin`
directamente al Release.

> Nota: si tu rama por defecto se llama `master` en vez de `main`, el
> workflow ya contempla ambos casos, no hace falta tocar nada.

## 📦 Compilar en local (alternativa)

Requisitos: Windows, Visual Studio 2022 (o `dotnet` CLI) con el workload de
escritorio .NET, y el propio repositorio de QuickLook (para referenciar
`QuickLook.Common`).

```powershell
# 1. Clona QuickLook.Common (o el monorepo QuickLook, que lo incluye)
git clone https://github.com/QL-Win/QuickLook.git

# 2. Clona/copia este plugin al lado
#    Estructura esperada:
#    /QuickLook/                        <- repo oficial
#    /QuickLook.Plugin.AffinityViewer/  <- este plugin

# 3. Ajusta la ruta del ProjectReference en el .csproj si tu estructura
#    de carpetas es distinta (por defecto asume "..\QuickLook\QuickLook.Common\...")

# 4. Compila
cd QuickLook.Plugin.AffinityViewer
dotnet build -c Release
```

Si no quieres clonar el repo completo, puedes en su lugar referenciar
directamente el `QuickLook.Common.dll` que ya tienes en tu instalación de
QuickLook (por defecto en `C:\Program Files\QuickLook\`). En el `.csproj` hay
un bloque comentado listo para eso: comenta el `ProjectReference` y
descomenta el `Reference` con `HintPath`.

## 🗜️ Empaquetar como `.qlplugin`

Un `.qlplugin` es simplemente un `.zip` renombrado que contiene la carpeta de
salida de la compilación (la DLL del plugin + dependencias):

```powershell
cd bin\Release\net8.0-windows
Compress-Archive -Path * -DestinationPath ..\..\..\QuickLook.Plugin.AffinityViewer.zip
Rename-Item ..\..\..\QuickLook.Plugin.AffinityViewer.zip QuickLook.Plugin.AffinityViewer.qlplugin
```

## 🔌 Instalar

1. Asegúrate de que QuickLook esté en ejecución (icono en la bandeja del sistema).
2. Ve a la carpeta donde quedó `QuickLook.Plugin.AffinityViewer.qlplugin`,
   selecciónalo y pulsa `Barra espaciadora`.
3. Haz clic en **Install** en la ventana que aparece.
4. Reinicia QuickLook (clic derecho en el icono de la bandeja → Quit, y
   vuelve a abrirlo).
5. Selecciona cualquier `.afphoto`/`.afdesign`/`.afpub` y pulsa espacio.

## Estructura del proyecto

```
QuickLook.Plugin.AffinityViewer/
├── Plugin.cs                    # Implementa IViewer (Init/CanHandle/Prepare/View/Cleanup)
├── AffinityFileSniffer.cs       # Verificación ligera de cabecera del archivo
├── ShellThumbnailProvider.cs    # Interop COM: pide la miniatura al Shell de Windows
├── EmbeddedPreviewExtractor.cs  # Respaldo: busca JPEG/PNG incrustado en el binario
├── AffinityPanel.xaml(.cs)      # UI: imagen con zoom/paneo + barra de estado
└── QuickLook.Plugin.AffinityViewer.csproj
```

## Ideas para mejorarlo

- Si algún día Affinity publica un SDK o especificación del formato, se
  podría sustituir `EmbeddedPreviewExtractor` por un decodificador real de
  capas/lienzo.
- Se podría cachear la miniatura extraída en disco para acelerar
  previsualizaciones repetidas de archivos grandes.
- Añadir un ajuste de configuración (tamaño máximo de miniatura, activar/desactivar
  el respaldo de escaneo binario) usando el sistema de ajustes de QuickLook.
