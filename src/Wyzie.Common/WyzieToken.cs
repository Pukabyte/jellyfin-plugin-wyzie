using System;
using System.Text;
using System.Text.Json;

namespace Wyzie.Common;

/// <summary>
/// Opaque token packed into RemoteSubtitleInfo.Id so GetSubtitles can stream
/// without re-hitting /search. Base64-url of a small JSON payload.
/// </summary>
public static class WyzieToken
{
    private sealed class Payload
    {
        public string U { get; set; } = string.Empty; // url
        public string F { get; set; } = "srt";         // format
        public string L { get; set; } = string.Empty; // language (3-letter)
    }

    public static string Encode(string url, string format, string language)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new Payload { U = url, F = format, L = language });
        return Convert.ToBase64String(json).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static bool TryDecode(string token, out string url, out string format, out string language)
    {
        url = format = language = string.Empty;
        try
        {
            var padded = token.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4) { case 2: padded += "=="; break; case 3: padded += "="; break; }
            var bytes = Convert.FromBase64String(padded);
            var p = JsonSerializer.Deserialize<Payload>(bytes);
            if (p is null || string.IsNullOrEmpty(p.U)) return false;
            url = p.U; format = p.F; language = p.L;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
