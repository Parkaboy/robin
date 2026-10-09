using Xunit;

public sealed class UiTextTests
{
    [Theory]
    [InlineData(UiLanguage.English, "About")]
    [InlineData(UiLanguage.Spanish, "Acerca de")]
    [InlineData(UiLanguage.German, "Über")]
    [InlineData(UiLanguage.Portuguese, "Sobre")]
    [InlineData(UiLanguage.Italian, "Informazioni")]
    public void AboutLabelIsAvailableInEverySupportedLanguage(UiLanguage language, string expected)
    {
        UiText.SetLanguage(language);
        try
        {
            Assert.Equal(expected, UiText.Get("About"));
            Assert.False(string.IsNullOrWhiteSpace(UiText.Get("LoadingData")));
        }
        finally
        {
            UiText.SetLanguage(UiLanguage.English);
        }
    }

    [Fact]
    public void GetThrowsWhenTranslationKeyDoesNotExist()
    {
        UiText.SetLanguage(UiLanguage.English);
        Assert.Throws<KeyNotFoundException>(() => UiText.Get("MissingTestTranslation"));
    }
}
