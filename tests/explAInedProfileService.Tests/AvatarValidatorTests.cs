public class AvatarValidatorTests
{
    private static byte[] Png(int padding = 16) =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[padding]];

    private static byte[] Jpeg(int padding = 16) => [0xFF, 0xD8, 0xFF, 0xE0, .. new byte[padding]];

    private static byte[] Webp()
    {
        byte[] bytes = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, .. new byte[16]];
        return bytes;
    }

    [Fact]
    public void AcceptsPngAndReportsItsType()
    {
        var result = AvatarValidator.Validate(Png(), "image/png");

        Assert.True(result.Ok);
        Assert.Equal("image/png", result.ContentType);
    }

    [Fact]
    public void AcceptsJpeg()
    {
        var result = AvatarValidator.Validate(Jpeg(), "image/jpeg");

        Assert.True(result.Ok);
        Assert.Equal("image/jpeg", result.ContentType);
    }

    [Fact]
    public void AcceptsWebp()
    {
        var result = AvatarValidator.Validate(Webp(), "image/webp");

        Assert.True(result.Ok);
        Assert.Equal("image/webp", result.ContentType);
    }

    [Fact]
    public void TreatsImageJpgAsJpeg()
    {
        var result = AvatarValidator.Validate(Jpeg(), "image/jpg");

        Assert.True(result.Ok);
        Assert.Equal("image/jpeg", result.ContentType);
    }

    [Fact]
    public void IgnoresContentTypeParameters()
    {
        var result = AvatarValidator.Validate(Png(), "image/png; charset=binary");

        Assert.True(result.Ok);
    }

    [Fact]
    public void SniffsWhenNoContentTypeIsDeclared()
    {
        var result = AvatarValidator.Validate(Png(), null);

        Assert.True(result.Ok);
        Assert.Equal("image/png", result.ContentType);
    }

    [Fact]
    public void RejectsDeclaredTypeThatContradictsTheBytes()
    {
        var result = AvatarValidator.Validate(Jpeg(), "image/png");

        Assert.False(result.Ok);
        Assert.Contains("does not match", result.Error);
    }

    [Fact]
    public void RejectsBytesThatAreNotAnImage()
    {
        var result = AvatarValidator.Validate("<html>not an image</html>"u8.ToArray(), "image/png");

        Assert.False(result.Ok);
        Assert.Contains("Unsupported image format", result.Error);
    }

    [Fact]
    public void RejectsEmptyContent()
    {
        var result = AvatarValidator.Validate([], "image/png");

        Assert.False(result.Ok);
        Assert.Contains("empty", result.Error);
    }

    [Fact]
    public void RejectsATruncatedPngSignature()
    {
        var result = AvatarValidator.Validate([0x89, 0x50, 0x4E], "image/png");

        Assert.False(result.Ok);
    }

    [Fact]
    public void RejectsRiffThatIsNotWebp()
    {
        byte[] riffWave = [.. "RIFF"u8, 0, 0, 0, 0, .. "WAVE"u8, .. new byte[8]];

        var result = AvatarValidator.Validate(riffWave, null);

        Assert.False(result.Ok);
    }
}
