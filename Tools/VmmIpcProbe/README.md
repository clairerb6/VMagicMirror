# VmmIpcProbe

CLI minima para probar IPC de VMagicMirror por transporte TCP (loopback), pensada para Linux.

## Requisitos

- .NET SDK instalado en tu maquina.
- Proceso Unity de VMagicMirror corriendo y usando `TcpIpcTransport`.

## Uso

```bash
dotnet run --project Tools/VmmIpcProbe -- \
  --mode command \
  --channel Baku.VMagicMirror \
  --command TopMost \
  --type Bool \
  --value true
```

Consulta (query) con espera de respuesta:

```bash
dotnet run --project Tools/VmmIpcProbe -- \
  --mode query \
  --channel Baku.VMagicMirror \
  --command CameraDeviceNames \
  --type None
```

## Parametros

- `--mode`: `command` o `query`
- `--channel`: channel id compartido con Unity (por defecto `Baku.VMagicMirror`)
- `--command`: nombre de `VmmCommands` (ej. `TopMost`, `MoveWindow`, `CameraDeviceNames`)
- `--type`: `None`, `Bool`, `Int`, `Float`, `String`
- `--value`: valor del payload cuando aplica

## Nota

La herramienta calcula automaticamente el puerto TCP a partir de `channelId` usando la misma formula que `TcpIpcTransport`.
