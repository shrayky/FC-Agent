using System.Buffers.Binary;
using System.Text;

namespace AtolLicenseSrvClient;

// Кадр AtolLicSvc: ГОСТ 28147-89, CFB, нестандартные S-блоки LicenseManager.
// Блок и ключ читаются little-endian — так же, как BitConverter в клиенте АТОЛ на Windows.
public static class AtolLicenseCipher
{
    private static readonly byte[] Pi =
    [
        0x01, 0x0f, 0x0d, 0x00, 0x05, 0x07, 0x0a, 0x04, 0x09, 0x02, 0x03, 0x0e, 0x06, 0x0b, 0x08, 0x0c,
        0x0d, 0x0b, 0x04, 0x01, 0x03, 0x0f, 0x05, 0x09, 0x00, 0x0a, 0x0e, 0x07, 0x06, 0x08, 0x02, 0x0c,
        0x04, 0x0b, 0x0a, 0x00, 0x07, 0x02, 0x01, 0x0d, 0x03, 0x06, 0x08, 0x05, 0x09, 0x0c, 0x0f, 0x0e,
        0x06, 0x0c, 0x07, 0x01, 0x05, 0x0f, 0x0d, 0x08, 0x04, 0x0a, 0x09, 0x0e, 0x00, 0x03, 0x0b, 0x02,
        0x07, 0x0d, 0x0a, 0x01, 0x00, 0x08, 0x09, 0x0f, 0x0e, 0x04, 0x06, 0x0c, 0x0b, 0x02, 0x05, 0x03,
        0x05, 0x08, 0x01, 0x0d, 0x0a, 0x03, 0x04, 0x02, 0x0e, 0x0f, 0x0c, 0x07, 0x06, 0x00, 0x09, 0x0b,
        0x0e, 0x0b, 0x04, 0x0c, 0x06, 0x0d, 0x0f, 0x0a, 0x02, 0x03, 0x08, 0x01, 0x00, 0x07, 0x05, 0x09,
        0x04, 0x0a, 0x09, 0x02, 0x0d, 0x08, 0x00, 0x0e, 0x06, 0x0b, 0x01, 0x0c, 0x07, 0x0f, 0x05, 0x03
    ];

    private static readonly uint[,] Substitution = new uint[4, 256];
    private static readonly uint[] Schedule = new uint[32];
    private static readonly byte[] Iv = [0x29, 0x28, 0x2a, 0x26, 0x5e, 0x25, 0x24, 0x23];

    static AtolLicenseCipher()
    {
        for (var i = 0; i < 256; i++)
        {
            var hi = i >> 4;
            var lo = i & 15;
            Substitution[0, i] = (uint)((Pi[hi] << 4) | Pi[16 + lo]);
            Substitution[1, i] = (uint)((Pi[32 + hi] << 4) | Pi[48 + lo]);
            Substitution[2, i] = (uint)((Pi[64 + hi] << 4) | Pi[80 + lo]);
            Substitution[3, i] = (uint)((Pi[96 + hi] << 4) | Pi[112 + lo]);
        }

        var key = new uint[8];
        var keyBytes = Encoding.ASCII.GetBytes("1qazxsw23edcvfr45tgbnhy67ujm,ki8");
        for (var i = 0; i < 8; i++)
            key[i] = BinaryPrimitives.ReadUInt32LittleEndian(keyBytes.AsSpan(i * 4));

        for (var i = 0; i < 24; i++)
            Schedule[i] = key[i % 8];
        for (var i = 0; i < 8; i++)
            Schedule[24 + i] = key[7 - i];
    }

    public static byte[] Encrypt(byte[] data)
    {
        var pad = (8 - data.Length % 8) % 8;
        var buf = new byte[data.Length + pad];
        data.CopyTo(buf, 0);

        var output = new byte[buf.Length];
        var feedback = (byte[])Iv.Clone();
        for (var offset = 0; offset < buf.Length; offset += 8)
        {
            var gamma = EncryptBlock(feedback);
            var cipher = Xor(buf.AsSpan(offset, 8), gamma);
            cipher.CopyTo(output.AsSpan(offset));
            feedback = cipher;
        }

        return output;
    }

    public static byte[] Decrypt(byte[] data)
    {
        var length = data.Length - data.Length % 8;
        var output = new byte[length];
        var feedback = (byte[])Iv.Clone();
        for (var offset = 0; offset < length; offset += 8)
        {
            var gamma = EncryptBlock(feedback);
            Xor(data.AsSpan(offset, 8), gamma).CopyTo(output.AsSpan(offset));
            feedback = data.AsSpan(offset, 8).ToArray();
        }

        return output;
    }

    // На выход блока CFB половины идут в порядке b, a — так считает LicenseManager.
    private static byte[] EncryptBlock(ReadOnlySpan<byte> block)
    {
        var a = BinaryPrimitives.ReadUInt32LittleEndian(block);
        var b = BinaryPrimitives.ReadUInt32LittleEndian(block[4..]);
        for (var round = 0; round < 32; round++)
        {
            var sum = Schedule[round] + a;
            var substituted = (Substitution[0, (int)(sum >> 24)] << 24)
                            | (Substitution[1, (int)((sum >> 16) & 255)] << 16)
                            | (Substitution[2, (int)((sum >> 8) & 255)] << 8)
                            | Substitution[3, (int)(sum & 255)];
            var nextA = Rol11(substituted) ^ b;
            b = a;
            a = nextA;
        }

        var result = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(result, b);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), a);
        return result;
    }

    private static uint Rol11(uint value) => (value << 11) | (value >> 21);

    private static byte[] Xor(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var result = new byte[8];
        for (var i = 0; i < 8; i++)
            result[i] = (byte)(left[i] ^ right[i]);
        return result;
    }
}
