using System.Buffers.Binary;
using System.Net.Sockets;
using Baku.VMagicMirror;
using Baku.VMagicMirror.IpcMessage;

const byte PacketTypeCommand = 0;
const byte PacketTypeQuery = 1;
const byte PacketTypeResponse = 2;

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp();
    return 0;
}

var opt = ParseArgs(args);
if (opt is null)
{
    PrintHelp();
    return 2;
}

var channel = string.IsNullOrWhiteSpace(opt.ChannelId) ? "Baku.VMagicMirror" : opt.ChannelId!;
var port = DerivePortFromChannelId(channel);
var payload = BuildPayload(opt);

using var client = new TcpClient();
await client.ConnectAsync("127.0.0.1", port);
using var stream = client.GetStream();

if (opt.Mode == Mode.Command)
{
    WritePacket(stream, PacketTypeCommand, 0, payload);
    Console.WriteLine($"sent command={opt.Command} type={opt.ValueType} port={port}");
    return 0;
}

ushort queryId = 1;
WritePacket(stream, PacketTypeQuery, queryId, payload);
Console.WriteLine($"sent query={opt.Command} type={opt.ValueType} port={port}, waiting response...");

while (true)
{
    var response = await ReadPacketAsync(stream, CancellationToken.None);
    if (response.Type != PacketTypeResponse || response.Id != queryId)
    {
        continue;
    }

    if (response.Body.Length >= 4)
    {
        var msgType = MessageDeserializer.GetValueType(response.Body);
        var text = msgType switch
        {
            MessageValueTypes.String => MessageDeserializer.ToString(response.Body),
            MessageValueTypes.Int => MessageDeserializer.ToInt(response.Body).ToString(),
            MessageValueTypes.Bool => MessageDeserializer.ToBool(response.Body).ToString(),
            _ => $"[binary len={response.Body.Length} type={msgType}]",
        };
        Console.WriteLine($"response: {text}");
    }
    else
    {
        Console.WriteLine("response: [empty]");
    }

    break;
}

return 0;

static ProbeOptions? ParseArgs(string[] args)
{
    var opt = new ProbeOptions();
    for (var i = 0; i < args.Length; i++)
    {
        var key = args[i];
        var next = i + 1 < args.Length ? args[i + 1] : "";
        switch (key)
        {
            case "--mode":
                if (!Enum.TryParse<Mode>(next, true, out var mode))
                {
                    return null;
                }
                opt.Mode = mode;
                i++;
                break;
            case "--channel":
                opt.ChannelId = next;
                i++;
                break;
            case "--command":
                if (!Enum.TryParse<VmmCommands>(next, true, out var command))
                {
                    return null;
                }
                opt.Command = command;
                i++;
                break;
            case "--type":
                if (!Enum.TryParse<MessageValueTypes>(next, true, out var valueType))
                {
                    return null;
                }
                opt.ValueType = valueType;
                i++;
                break;
            case "--value":
                opt.Value = next;
                i++;
                break;
        }
    }

    return opt.Command == VmmCommands.Unknown ? null : opt;
}

static byte[] BuildPayload(ProbeOptions opt)
{
    var commandId = (ushort)opt.Command;
    return opt.ValueType switch
    {
        MessageValueTypes.None => MessageSerializer.None(commandId),
        MessageValueTypes.Bool => MessageSerializer.Bool(commandId, bool.TryParse(opt.Value, out var b) && b),
        MessageValueTypes.Int => MessageSerializer.Int(commandId, int.TryParse(opt.Value, out var i) ? i : 0),
        MessageValueTypes.Float => MessageSerializer.Float(commandId, float.TryParse(opt.Value, out var f) ? f : 0f),
        MessageValueTypes.String => MessageSerializer.String(commandId, opt.Value ?? string.Empty),
        _ => throw new NotSupportedException($"type not supported by probe: {opt.ValueType}")
    };
}

static int DerivePortFromChannelId(string channelId)
{
    var hash = 17;
    foreach (var c in channelId)
    {
        hash = (hash * 31) + c;
    }
    var positive = hash == int.MinValue ? int.MaxValue : Math.Abs(hash);
    return 35000 + (positive % 20000);
}

static void WritePacket(NetworkStream stream, byte type, ushort id, ReadOnlySpan<byte> body)
{
    Span<byte> header = stackalloc byte[7];
    header[0] = type;
    BinaryPrimitives.WriteUInt16LittleEndian(header[1..3], id);
    BinaryPrimitives.WriteInt32LittleEndian(header[3..7], body.Length);
    stream.Write(header);
    if (body.Length > 0)
    {
        stream.Write(body);
    }
    stream.Flush();
}

static async Task<Packet> ReadPacketAsync(NetworkStream stream, CancellationToken ct)
{
    var header = new byte[7];
    await ReadExactAsync(stream, header, ct);
    var type = header[0];
    var id = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(1, 2));
    var len = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(3, 4));
    if (len < 0)
    {
        throw new InvalidOperationException("Negative payload length");
    }

    var body = new byte[len];
    if (len > 0)
    {
        await ReadExactAsync(stream, body, ct);
    }
    return new Packet(type, id, body);
}

static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
{
    var offset = 0;
    while (offset < buffer.Length)
    {
        var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
        if (read <= 0)
        {
            throw new IOException("Socket closed while reading");
        }
        offset += read;
    }
}

static void PrintHelp()
{
    Console.WriteLine("""
vmm-ipc-probe
Usage:
  dotnet run --project Tools/VmmIpcProbe -- --mode command --channel <id> --command <VmmCommands> --type <None|Bool|Int|Float|String> [--value <value>]
  dotnet run --project Tools/VmmIpcProbe -- --mode query   --channel <id> --command <VmmCommands> --type <...> [--value <value>]

Examples:
  dotnet run --project Tools/VmmIpcProbe -- --mode command --channel Baku.VMagicMirror --command TopMost --type Bool --value true
  dotnet run --project Tools/VmmIpcProbe -- --mode command --channel Baku.VMagicMirror --command MoveWindow --type String --value "100,100"
""");
}

enum Mode
{
    Command,
    Query
}

sealed class ProbeOptions
{
    public Mode Mode { get; set; } = Mode.Command;
    public string? ChannelId { get; set; }
    public VmmCommands Command { get; set; } = VmmCommands.Unknown;
    public MessageValueTypes ValueType { get; set; } = MessageValueTypes.None;
    public string? Value { get; set; }
}

readonly record struct Packet(byte Type, ushort Id, byte[] Body);
