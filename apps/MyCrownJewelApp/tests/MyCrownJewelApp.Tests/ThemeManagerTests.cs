using Xunit;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

public class ThemeManagerTests
{
    [Fact]
    public void Instance_IsNotNull() => Assert.NotNull(ThemeManager.Instance);

    [Fact]
    public void Themes_ContainsDark() => Assert.True(ThemeManager.Themes.ContainsKey("Dark"));

    [Fact]
    public void Themes_ContainsLight() => Assert.True(ThemeManager.Themes.ContainsKey("Light"));

    [Fact]
    public void ThemeNames_IncludesDark() => Assert.Contains("Dark", ThemeManager.ThemeNames);

    [Fact]
    public void SetTheme_UpdatesCurrentTheme()
    {
        ThemeManager.Instance.SetTheme("Light");
        Assert.Equal("Light", ThemeManager.Instance.CurrentTheme.Name);
        ThemeManager.Instance.SetTheme("Dark");
    }

    [Fact]
    public void SetTheme_UnknownName_DoesNotChangeCurrent()
    {
        ThemeManager.Instance.SetTheme("Dark");
        ThemeManager.Instance.SetTheme("NonExistentTheme");
        Assert.Equal("Dark", ThemeManager.Instance.CurrentTheme.Name);
    }

    [Fact]
    public void SetTheme_FiresThemeChangedEvent()
    {
        bool fired = false;
        void Handler(Theme _) => fired = true;

        ThemeManager.Instance.SetTheme("Dark");
        ThemeManager.Instance.ThemeChanged += Handler;
        try
        {
            ThemeManager.Instance.SetTheme("Light");
            Assert.True(fired);
        }
        finally
        {
            ThemeManager.Instance.ThemeChanged -= Handler;
            ThemeManager.Instance.SetTheme("Dark");
        }
    }

    [Fact]
    public void Theme_Dark_IsNotLight() => Assert.False(Theme.Dark.IsLight);

    [Fact]
    public void Theme_Light_IsLight() => Assert.True(Theme.Light.IsLight);

    [Fact]
    public void Theme_HasNonEmptyName() => Assert.NotEmpty(Theme.Dark.Name);
}
