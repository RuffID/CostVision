namespace CostVision.Infrastructure.IntegrationTests.Web.Helpers;

public static class CookieHeader
{
    public static string Merge(string? currentHeader, HttpResponseMessage response)
    {
        Dictionary<string, string> cookies = Parse(currentHeader);

        if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookieHeaders))
        {
            foreach (string setCookieHeader in setCookieHeaders)
            {
                string cookie = setCookieHeader.Split(';', 2)[0];
                int separatorIndex = cookie.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                cookies[cookie[..separatorIndex]] = cookie[(separatorIndex + 1)..];
            }
        }

        return string.Join("; ", cookies.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static Dictionary<string, string> Parse(string? cookieHeader)
    {
        Dictionary<string, string> cookies = new(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(cookieHeader))
            return cookies;

        foreach (string part in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            cookies[part[..separatorIndex]] = part[(separatorIndex + 1)..];
        }

        return cookies;
    }
}
