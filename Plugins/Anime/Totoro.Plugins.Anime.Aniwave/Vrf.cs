using System.Text;
using System.Text.Json.Nodes;
using System.Web;
using Flurl.Http;

namespace Totoro.Plugins.Anime.Aniwave;

internal static class Vrf
{
    internal static Task<string> Encode(string text)
    {
        var vrf = RC4.Transform(text, "ysJhV6U27FVIjjuk").ToArray();
        byte[] unsigned = (byte[])(Array)vrf;
        var b64s = Convert.ToBase64String(unsigned.ToArray(), Base64FormattingOptions.None);
        var b64b = Encoding.UTF8.GetBytes(b64s);
        b64s = Convert.ToBase64String(b64b, Base64FormattingOptions.None);
        b64b = Encoding.UTF8.GetBytes(b64s);
        VrfShift(b64b);
        b64s = Convert.ToBase64String(b64b);
        b64b = Encoding.UTF8.GetBytes(b64s);
        Rot13(b64b);
        var vrfs = Encoding.UTF8.GetString(b64b);
        return Task.FromResult(HttpUtility.UrlEncode(vrfs));
    }


    internal static Task<string> Decode(string text)
    {
        var bytes = Convert.FromBase64String(text.Replace("-", "+").Replace("_", "/"));
        var key = Encoding.UTF8.GetBytes("hlPeNwkncH0fq9so");
        var decryptedBytes = RC42.Decrypt(key, bytes);
        var decrypted = Encoding.UTF8.GetString(decryptedBytes);
        return Task.FromResult(HttpUtility.UrlDecode(decrypted));
    }

    private static void Rot13(byte[] vrf)
    {
        for (int i = 0; i < vrf.Length; i++)
        {
            byte currentByte = vrf[i];

            if (currentByte >= 'A' && currentByte <= 'Z')
            {
                vrf[i] = (byte)(((currentByte - 'A' + 13) % 26) + 'A');
            }
            else if (currentByte >= 'a' && currentByte <= 'z')
            {
                vrf[i] = (byte)(((currentByte - 'a' + 13) % 26) + 'a');
            }
        }
    }

    private static void VrfShift(byte[] vrf)
    {
        int[] shifts = [-3, 3, -4, 2, -2, 5, 4, 5];

        for (int i = 0; i < vrf.Length; i++)
        {
            int shift = shifts[i % 8];
            vrf[i] = (byte)(vrf[i] + shift);
        }
    }
}

public static class RC42
{
    public static string Encrypt(string key, string data)
    {
        Encoding unicode = Encoding.Unicode;

        return Convert.ToBase64String(Encrypt(unicode.GetBytes(key), unicode.GetBytes(data)));
    }

    public static string Decrypt(string key, string data)
    {
        Encoding unicode = Encoding.Unicode;

        return unicode.GetString(Encrypt(unicode.GetBytes(key), Convert.FromBase64String(data)));
    }

    public static byte[] Encrypt(byte[] key, byte[] data)
    {
        return EncryptOutput(key, data).ToArray();
    }

    public static byte[] Decrypt(byte[] key, byte[] data)
    {
        return EncryptOutput(key, data).ToArray();
    }

    private static byte[] EncryptInitalize(byte[] key)
    {
        byte[] s = Enumerable.Range(0, 256)
          .Select(i => (byte)i)
          .ToArray();

        for (int i = 0, j = 0; i < 256; i++)
        {
            j = (j + key[i % key.Length] + s[i]) & 255;

            Swap(s, i, j);
        }

        return s;
    }

    private static IEnumerable<byte> EncryptOutput(byte[] key, IEnumerable<byte> data)
    {
        byte[] s = EncryptInitalize(key);

        int i = 0;
        int j = 0;

        return data.Select((b) =>
        {
            i = (i + 1) & 255;
            j = (j + s[i]) & 255;

            Swap(s, i, j);

            return (byte)(b ^ s[(s[i] + s[j]) & 255]);
        });
    }

    private static void Swap(byte[] s, int i, int j)
    {
        byte c = s[i];

        s[i] = s[j];
        s[j] = c;
    }
}

public static class RC4
{
    public const int KeySize = 256;
    
    public static List<sbyte> Transform(string text, string password)
    {
        var sbox = new int[KeySize];
        int[] key = new int[KeySize];
        int n = password.Length;
        for (int a = 0; a < KeySize; a++)
        {
            key[a] = password[a % n];
            sbox[a] = a;
        }

        int b = 0;
        for (int a = 0; a < KeySize; a++)
        {
            b = (b + sbox[a] + key[a]) % KeySize;
            (sbox[b], sbox[a]) = (sbox[a], sbox[b]);
        }

        int i = 0, j = 0;
        List<sbyte> cipher = [];
        for (int a = 0; a < text.Length; a++)
        {
            i = (i + 1) % KeySize;
            j = (j + sbox[i]) % KeySize;
            (sbox[j], sbox[i]) = (sbox[i], sbox[j]);

            int k = sbox[(sbox[i] + sbox[j]) % KeySize];
            int cipherBy = text[a] ^ k;  //xor operation
            cipher.Add((sbyte)cipherBy);
        }

        return cipher;
    }
}
