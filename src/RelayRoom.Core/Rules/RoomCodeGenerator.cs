using System.Security.Cryptography;

namespace RelayRoom.Core.Rules;

/// <summary>
/// Crockford Base32 without I, L, O, U so codes are easy to read and type.
/// </summary>
public static class RoomCodeGenerator
{
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Create(int length = RoomLimits.PublicCodeLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);

        Span<char> chars = stackalloc char[length];
        Span<byte> bytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(bytes);

        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }

    public static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var buffer = new char[code.Length];
        var written = 0;

        foreach (var raw in code)
        {
            if (char.IsWhiteSpace(raw) || raw == '-')
            {
                continue;
            }

            var c = char.ToUpperInvariant(raw);
            c = c switch
            {
                'I' or 'L' => '1',
                'O' => '0',
                'U' => 'V',
                _ => c
            };

            buffer[written++] = c;
        }

        return new string(buffer, 0, written);
    }

    public static bool IsValid(string code)
    {
        if (code.Length != RoomLimits.PublicCodeLength)
        {
            return false;
        }

        foreach (var c in code)
        {
            if (!Alphabet.Contains(c))
            {
                return false;
            }
        }

        return true;
    }
}
