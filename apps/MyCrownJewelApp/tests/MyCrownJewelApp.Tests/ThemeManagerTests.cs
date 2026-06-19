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
        var manager = ThemeManager.Instance;
        string originalTheme = manager.CurrentTheme.Name;

        try
        {
            manager.SetTheme("Light");
            Assert.Equal("Light", manager.CurrentTheme.Name);
        }
        finally
        {
            manager.SetTheme(originalTheme);
        }
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
        var manager = ThemeManager.Instance;
        string originalTheme = manager.CurrentTheme.Name;
        bool fired = false;
        void Handler(Theme _) => fired = true;

        manager.ThemeChanged += Handler;
        try
        {
            manager.SetTheme(originalTheme == "Light" ? "Dark" : "Light");
            Assert.True(fired);
        }
        finally
        {
            manager.ThemeChanged -= Handler;
            manager.SetTheme(originalTheme);
        }
    }

    [Fact]
    public void Theme_Dark_IsNotLight() => Assert.False(Theme.Dark.IsLight);

    [Fact]
    public void Theme_Light_IsLight() => Assert.True(Theme.Light.IsLight);

    [Fact]
    public void Theme_HasNonEmptyName() => Assert.NotEmpty(Theme.Dark.Name);
}
