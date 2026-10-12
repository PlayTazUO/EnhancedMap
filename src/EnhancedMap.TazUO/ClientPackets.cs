using System.Text;

namespace Assistant;

/// <summary>
///     Helpers for injecting server-style packets into the running client (via Engine.SendToClient).
///     ClassicUO framing for both is variable length: [id][lenHi][lenLo][payload...].
/// </summary>
internal static class ClientPackets
{
    // Custom 64-bit prompt tokens so we can recognize our own prompt responses.
    public const ulong TokenAddress = 0x454D415F41444452; // "EMA_ADDR"
    public const ulong TokenPort = 0x454D415F504F5254; // "EMA_PORT"

    /// <summary>Shows a system/ASCII message in the client (packet 0x1C).</summary>
    public static void AsciiMessage(string text, string name = "EnhancedMap", ushort hue = 0x35, ushort font = 3, byte type = 1)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var body = new List<byte>(64);

        WriteU32BE(body, 0); // serial
        WriteU16BE(body, 1); // graphic (unused by client)
        body.Add(type);
        WriteU16BE(body, hue);
        WriteU16BE(body, font);

        byte[] nameBytes = Encoding.ASCII.GetBytes(name.Length > 30 ? name[..30] : name);
        for (int i = 0; i < 30; i++)
            body.Add(i < nameBytes.Length ? nameBytes[i] : (byte)0);

        body.AddRange(Encoding.ASCII.GetBytes(text));

        Send(0x1C, body);
    }

    /// <summary>Injects a Unicode prompt request (packet 0xC2) so the client shows a text input.</summary>
    public static void UnicodePrompt(ulong token)
    {
        var body = new List<byte>(8);
        WriteU64BE(body, token);
        Send(0xC2, body);
    }

    private static void Send(byte id, List<byte> body)
    {
        int total = body.Count + 3;
        var packet = new byte[total];
        packet[0] = id;
        packet[1] = (byte)((total >> 8) & 0xff);
        packet[2] = (byte)(total & 0xff);
        body.CopyTo(packet, 3);

        int length = packet.Length;
        Engine.SendToClient(ref packet, ref length);
    }

    private static void WriteU16BE(List<byte> b, ushort v)
    {
        b.Add((byte)(v >> 8));
        b.Add((byte)v);
    }

    private static void WriteU32BE(List<byte> b, uint v)
    {
        b.Add((byte)(v >> 24));
        b.Add((byte)(v >> 16));
        b.Add((byte)(v >> 8));
        b.Add((byte)v);
    }

    private static void WriteU64BE(List<byte> b, ulong v)
    {
        for (int i = 7; i >= 0; i--)
            b.Add((byte)(v >> (i * 8)));
    }
}
