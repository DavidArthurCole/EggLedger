using EggLedger.Web.Missions;

namespace EggLedger.Web.Tests.Missions;

public class CustomCssSanitizerTests {
    [Theory]
    [InlineData("color: red;")]
    [InlineData("background: url(data:image/png;base64,abcd);")]
    public void TryScope_AllowsCleanInput(string css) {
        bool ok = CustomCssSanitizer.TryScope("MyPreset", css, out string? scoped, out string? error);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Contains(".mission-card-custom-", scoped);
        Assert.Contains(css, scoped);
    }

    [Fact]
    public void TryScope_NullOrBlank_ReturnsTrueWithNullScopedCss() {
        bool ok = CustomCssSanitizer.TryScope("MyPreset", null, out string? scoped, out string? error);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Null(scoped);
    }

    [Theory]
    [InlineData("@import url(evil.css);")]
    [InlineData("width: expression(alert(1));")]
    [InlineData("background: url(javascript:alert(1));")]
    [InlineData("background: url(https://evil.com/x.png);")]
    [InlineData("content: '</style><script>alert(1)</script>';")]
    [InlineData(@"@\69mport ""https://evil.com/x"";")]
    [InlineData(@"background: \75rl(https://evil.com/x.png);")]
    public void TryScope_RejectsDangerousPatterns(string css) {
        bool ok = CustomCssSanitizer.TryScope("MyPreset", css, out string? scoped, out string? error);
        Assert.False(ok);
        Assert.Null(scoped);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryScope_AllowsLegitimateEscapeSequence() {
        string css = @"content: ""\2014"";";
        bool ok = CustomCssSanitizer.TryScope("MyPreset", css, out string? scoped, out string? error);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Contains(css, scoped);
    }

    [Theory]
    [InlineData("My  Preset", "my-preset")]
    [InlineData("Weekend (v2)", "weekend-v2")]
    [InlineData("Classic", "classic")]
    public void SlugFor_CollapsesRunsOfSpecialChars_MatchingScopedClassName(string presetName, string expectedSlug) {
        string slug = CustomCssSanitizer.SlugFor(presetName);
        Assert.Equal(expectedSlug, slug);

        CustomCssSanitizer.TryScope(presetName, "color: red;", out string? scoped, out _);
        Assert.Contains($".mission-card-custom-{expectedSlug} ", scoped);
    }

    [Fact]
    public void TryScope_RejectsOversizedInput() {
        string css = new('a', 20_001);
        bool ok = CustomCssSanitizer.TryScope("MyPreset", css, out string? scoped, out string? error);
        Assert.False(ok);
        Assert.Null(scoped);
        Assert.Contains("20", error);
    }
}
