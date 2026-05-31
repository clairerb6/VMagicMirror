# VMagicMirror Linux Porting Backlog

Este documento es el backlog vivo para migrar VMagicMirror desde una base Windows-only hacia una aplicacion portable para Linux. La idea es mantener aqui el plan, decisiones tecnicas y avances reales a medida que se implementen.

## Objetivo

Lograr una version Linux ejecutable y mantenible de VMagicMirror, preservando primero el nucleo de Unity y luego recuperando gradualmente las funciones dependientes de plataforma: ventana transparente, click-through, input global, MIDI, gamepad, configuracion externa y salida para streaming.

## Principios

- Separar codigo multiplataforma de codigo especifico de Windows mediante interfaces claras.
- Mantener Windows funcionando durante la migracion.
- Priorizar un primer build Linux usable aunque tenga funciones degradadas.
- Documentar cada reemplazo de API nativa con su decision y limitaciones.
- Evitar reescrituras grandes si una capa de compatibilidad permite avanzar con menos riesgo.

## Estado Inicial

### Componentes Windows-only detectados

- UI de configuracion WPF:
  - `WPF/VMagicMirrorConfig/VMagicMirrorConfig.csproj`
  - Target actual: `net8.0-windows`
  - Usa `UseWpf`, XAML, MahApps, MaterialDesignThemes y APIs `System.Windows`.
- Control nativo de ventana Unity:
  - `VMagicMirror/Assets/Baku/VMagicMirror/Scripts/NativeWindow/NativeMethods.cs`
  - `VMagicMirror/Assets/Baku/VMagicMirror/Scripts/NativeWindow/WindowStyleController.cs`
  - Usa `user32.dll`, `Dwmapi.dll`, estilos Win32, topmost, transparencia, posicion, alpha y click-through.
- Input global y RawInput:
  - `VMagicMirror/Assets/Baku/VMagicMirror/Scripts/InputMonitoring/KeyAndMouseInput`
  - Usa RawInput, hooks de teclado/mouse y `System.Windows.Forms.Keys`.
- Gamepad:
  - `VMagicMirror/Assets/Baku/VMagicMirror/Scripts/InputMonitoring/GamePadInput/XInputCapture.cs`
  - Usa `Xinput1_4.dll`.
- MIDI:
  - `VMagicMirror/Assets/Baku/VMagicMirror/Scripts/InputMonitoring/MidiInput/MidiControlReceiver.cs`
  - Usa `MidiJack.WindowsMidiInterop`.
- Texture sharing:
  - `VMagicMirror/Assets/Baku/VMagicMirror/Scripts/TextureSharing`
  - Usa Spout via `Klak.Spout`, orientado a Windows.
- IPC entre Unity y configurador:
  - `VMagicMirror/Assets/Baku/VMagicMirror/Scripts/Interprocess/MemoryMappedFile`
  - Usa `System.IO.MemoryMappedFiles`; puede requerir validacion o backend alternativo en Linux.
- Build tooling:
  - `Batches/*.cmd`
  - `Batches/*.ps1`
  - `BuildHelper.cs` apunta a `StandaloneWindows64`.

### Componentes probablemente portables

- Carga VRM y logica de avatar.
- Shaders y postprocesado, sujeto a validacion grafica en Linux.
- Sistema de mensajes IPC a nivel de protocolo.
- Configuraciones serializadas y modelos de datos.
- Funciones de red para trackers externos, sujeto a firewall/permisos.

## Fase 0: Inventario y Estrategia

Estado: en progreso

### Tareas

- [x] Detectar referencias directas a Win32, WPF, RawInput, XInput, MIDI Windows y Spout.
- [x] Separar los principales bloqueadores por area funcional.
- [x] Crear matriz de features: imprescindible, degradable, opcional.
- [x] Definir version minima de Linux objetivo.
- [x] Definir backend grafico objetivo: X11, Wayland o ambos con limitaciones.
- [x] Definir version exacta de Unity usada para el port Linux.

### Decisiones Fase 0 (MVP Linux)

- Version de Unity base del port:
  - `6000.0.58f2` (tomada de `VMagicMirror/ProjectSettings/ProjectVersion.txt`).
- Linux minimo objetivo para pruebas y soporte inicial:
  - Ubuntu 22.04 LTS x86_64.
- Backend grafico objetivo:
  - Objetivo primario: X11.
  - Wayland: soporte best-effort con degradacion esperada en click-through/input global.

### Matriz de features (primer build experimental)

- Imprescindible (debe funcionar en MVP):
  - Arranque de Unity en `StandaloneLinux64`.
  - Carga de avatar VRM.
  - Render basico del avatar y escena principal.
  - IPC minimo para recibir comandos externos basicos.
  - Configuracion por archivo (lectura/escritura).
- Degradable (puede salir con no-op o fallback):
  - Ventana transparente, click-through y topmost avanzado.
  - Input global fuera de foco.
  - Gamepad avanzado por backend especifico.
  - MIDI.
  - Integracion de streaming tipo Spout.
- Opcional (post-MVP):
  - Paridad visual completa entre compositores Linux.
  - Integraciones avanzadas para OBS/PipeWire.
  - Optimizaciones finas de UX del configurador Linux.

### Criterio de completado

- Existe una lista priorizada de areas a migrar.
- Hay una decision escrita sobre el primer entorno Linux soportado.
- Se sabe que features entran en el primer build experimental.

## Fase 1: Build Unity Linux con Funciones Degradadas

Estado: en progreso

Objetivo: conseguir que el proyecto Unity compile para `StandaloneLinux64` aunque algunos servicios de plataforma sean no-op.

### Tareas

- [x] Crear abstraccion para control de ventana, por ejemplo `IPlatformWindow`.
- [x] Mover llamadas Win32 actuales a implementacion `WindowsPlatformWindow`.
- [x] Crear `LinuxPlatformWindow` inicial con stubs seguros.
- [x] Aislar `NativeMethods.cs` detras de compilacion condicional Windows.
- [x] Reemplazar usos directos de `System.Windows.Forms` en runtime Unity o encapsularlos.
- [x] Ajustar `BuildHelper.cs` para aceptar `StandaloneLinux64`.
- [ ] Validar que Unity abre escenas principales sin errores de compilacion.

### Criterio de completado

- Unity compila en Linux.
- La aplicacion arranca con una ventana normal.
- Las funciones no soportadas quedan desactivadas de forma explicita, sin excepciones por `DllNotFoundException`.

## Fase 2: IPC Multiplataforma

Estado: pendiente

Objetivo: mantener el protocolo de mensajes y permitir comunicacion entre Unity y la futura app de configuracion Linux.

### Tareas

- [x] Crear interfaz de transporte IPC, por ejemplo `IIpcTransport`.
- [x] Encapsular `MemoryMappedFileConnector` como backend actual.
- [ ] Investigar comportamiento real de `System.IO.MemoryMappedFiles` entre procesos en Linux.
- [x] Probar backend alternativo con named pipes o socket local.
- [ ] Mantener compatibilidad con mensajes actuales `VmmCommands` y `MessageFactory`.
- [x] Agregar prueba o herramienta minima que envie comandos entre dos procesos.

### Criterio de completado

- Unity puede recibir comandos desde un proceso externo en Linux.
- Windows conserva el IPC actual o una ruta compatible.
- El protocolo no se reescribe innecesariamente.

## Fase 3: Reemplazo de WPF

Estado: pendiente

Objetivo: reemplazar la app WPF por una UI multiplataforma.

### Decision candidata

Avalonia es la opcion inicial recomendada porque permite mantener C#, MVVM, bindings y parte de los modelos actuales. La decision final queda pendiente hasta revisar cuanto codigo de ViewModel puede reutilizarse sin arrastrar `System.Windows`.

### Tareas

- [ ] Inventariar ViewModels reutilizables y dependencias WPF.
- [ ] Separar modelos/configuracion de la capa `View`.
- [ ] Crear proyecto experimental Avalonia.
- [ ] Conectar UI experimental al IPC multiplataforma.
- [ ] Portar primero controles esenciales:
  - Home/start/stop.
  - Carga de VRM.
  - Transparencia/fondo.
  - Topmost/frame/window size, con degradacion Linux.
  - Input basico.
- [ ] Migrar localizaciones relevantes.

### Criterio de completado

- Existe configurador Linux capaz de iniciar Unity y cambiar opciones basicas.
- Windows puede seguir usando WPF durante la transicion o migrarse despues.
- La UI nueva no depende de `net*-windows`.

## Fase 4: Input Multiplataforma

Estado: pendiente

Objetivo: reemplazar RawInput, hooks Win32 y XInput por backends portables.

### Tareas

- [ ] Definir interfaz de input para teclado, mouse, gamepad y eventos globales.
- [ ] Evaluar Unity Input System para input en foco.
- [ ] Evaluar SDL, evdev, libinput o backend nativo para input global Linux.
- [ ] Implementar gamepad con Unity Input System o SDL.
- [ ] Reemplazar `System.Windows.Forms.Keys` por enum propio o mapeo portable.
- [ ] Documentar limitaciones Wayland para input global.

### Criterio de completado

- Teclado y mouse funcionan al menos cuando la app tiene foco.
- Gamepad funciona en Linux.
- Las funciones globales quedan soportadas o degradadas segun backend disponible.

## Fase 5: Ventana Transparente, Click-through y Topmost

Estado: pendiente

Objetivo: recuperar el comportamiento de mascota de escritorio en Linux.

### Tareas

- [ ] Probar soporte de ventana transparente Unity Linux.
- [ ] Investigar click-through en X11.
- [ ] Investigar limitaciones en Wayland.
- [ ] Implementar backend X11 si es viable.
- [ ] Agregar deteccion de entorno y fallback.
- [ ] Documentar diferencias de comportamiento frente a Windows.

### Criterio de completado

- En X11, la ventana puede ser transparente y click-through si el compositor lo permite.
- En Wayland, el comportamiento queda documentado y degradado limpiamente si no es posible.
- No se rompe la experiencia de ventana normal.

## Fase 6: MIDI y Streaming

Estado: pendiente

Objetivo: recuperar funciones de dispositivos y salida para OBS/streaming con alternativas Linux.

### Tareas

- [ ] Reemplazar `WindowsMidiInterop` por backend MIDI multiplataforma.
- [ ] Evaluar RtMidi, PortMidi, ALSA MIDI o plugin Unity mantenido.
- [ ] Reemplazar Spout por alternativa Linux:
  - PipeWire.
  - OBS capture.
  - NDI.
  - Syphon no aplica para Linux.
  - RenderTexture export o virtual camera si es viable.
- [ ] Separar UI de Spout para que no aparezca como opcion Linux si no corresponde.

### Criterio de completado

- MIDI funciona en Linux con al menos dispositivos ALSA/JACK comunes.
- Existe una ruta recomendada para captura en OBS.
- La UI muestra opciones coherentes por plataforma.

## Fase 7: Packaging y Distribucion

Estado: pendiente

Objetivo: entregar builds Linux reproducibles.

### Tareas

- [ ] Crear script de build Linux.
- [ ] Definir formato de distribucion: tar.gz, AppImage, Flatpak o paquete distro.
- [ ] Incluir configurador multiplataforma.
- [ ] Documentar dependencias del sistema.
- [ ] Probar en una distro objetivo.

### Criterio de completado

- Hay un artefacto Linux instalable o ejecutable.
- Instrucciones de ejecucion y troubleshooting estan documentadas.
- El build es reproducible desde el repo.

## Matriz de Features

Estado: borrador pendiente de completar.

| Feature | Windows actual | Linux primer build | Linux objetivo | Notas |
| --- | --- | --- | --- | --- |
| Mostrar avatar VRM | Si | Si | Si | Prioridad maxima |
| Configuracion externa | WPF | Pendiente | Avalonia u otra UI | WPF no portable |
| Ventana transparente | Win32/DWM | No | X11 posible | Wayland limitado |
| Click-through | Win32 | No | X11 posible | Wayland limitado |
| Topmost | Win32 | Degradado | Segun backend | Depende de WM/compositor |
| Input teclado/mouse | RawInput/hooks | En foco | Global si viable | Wayland limita global input |
| Gamepad | XInput | Pendiente | Unity Input/SDL | Reemplazar XInput |
| MIDI | Windows MIDI | Pendiente | ALSA/JACK/RtMidi | Reemplazar backend |
| Spout | Si | No | Alternativa Linux | PipeWire/OBS/NDI |
| IPC | MMF | Pendiente validar | Socket/pipe/MMF | Mantener protocolo |

## Registro de Avances

### 2026-04-28

- Se creo este backlog inicial de migracion Linux.
- Se identificaron los principales acoplamientos Windows:
  - WPF para configuracion.
  - Win32/DWM para ventana.
  - RawInput/hooks para teclado y mouse.
  - XInput para gamepad.
  - Windows MIDI.
  - Spout para texture sharing.
  - Scripts de build Windows.
- Se propuso iniciar por un build Unity Linux con stubs de plataforma antes de reescribir la UI completa.

## Decisiones Abiertas

- Distro Linux objetivo para pruebas iniciales.
- Soporte oficial para X11, Wayland o ambos.
- UI multiplataforma final: Avalonia u otra alternativa.
- Transporte IPC final.
- Nivel minimo aceptable para el primer build experimental.
