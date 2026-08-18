public static class AvatarValidator
{
    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";
    public const string Webp = "image/webp";

    public readonly record struct Result(string? ContentType, string? Error)
    {
        public bool Ok => Error is null;
    }

    public static Result Validate(byte[] content, string? declaredContentType)
    {
        if (content.Length == 0)
        {
            return new Result(null, "Avatar is empty");
        }

        var sniffed = Sniff(content);
        if (sniffed is null)
        {
            return new Result(null, $"Unsupported image format (expected {Png}, {Jpeg} or {Webp})");
        }

        var declared = Canonical(declaredContentType);
        if (declared is not null && declared != sniffed)
        {
            return new Result(null, $"Content-Type {declared} does not match the uploaded bytes ({sniffed})");
        }

        return new Result(sniffed, null);
    }

    private static string? Sniff(byte[] content)
    {
        if (content.Length >= 8
            && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47
            && content[4] == 0x0D && content[5] == 0x0A && content[6] == 0x1A && content[7] == 0x0A)
        {
            return Png;
        }

        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return Jpeg;
        }

        if (content.Length >= 12
            && content[0] == (byte)'R' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'F'
            && content[8] == (byte)'W' && content[9] == (byte)'E' && content[10] == (byte)'B' && content[11] == (byte)'P')
        {
            return Webp;
        }

        return null;
    }

    private static string? Canonical(string? declared)
    {
        if (string.IsNullOrWhiteSpace(declared)) return null;

        var value = declared.Split(';')[0].Trim().ToLowerInvariant();

        return value switch
        {
            Png or Jpeg or Webp => value,
            "image/jpg" => Jpeg,
            _ => value,
        };
    }
}
