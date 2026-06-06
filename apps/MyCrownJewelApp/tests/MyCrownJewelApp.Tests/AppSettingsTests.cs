using Xunit;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

public class AppSettingsTests
{
    [Fact] public void Default_ThemeName_IsDark() => Assert.Equal("Dark", new AppSettings().ThemeName);
    [Fact] public void Default_GutterVisible_IsTrue() => Assert.True(new AppSettings().GutterVisible);
    [Fact] public void Default_StatusBarVisible_IsTrue() => Assert.True(new AppSettings().StatusBarVisible);
    [Fact] public void Default_TabSize_IsFour() => Assert.Equal(4, new AppSettings().TabSize);
    [Fact] public void Default_VimModeEnabled_IsFalse() => Assert.False(new AppSettings().VimModeEnabled);
    [Fact] public void Default_StickyScrollEnabled_IsTrue() => Assert.True(new AppSettings().StickyScrollEnabled);
    [Fact] public void Default_AnalyzersEnabled_IsTrue() => Assert.True(new AppSettings().AnalyzersEnabled);
    [Fact] public void Default_AutoSaveEnabled_IsFalse() => Assert.False(new AppSettings().AutoSaveEnabled);

    [Fact]
    public void InitProperty_WordWrap_Roundtrips()
    {
        var s = new AppSettings { WordWrapEnabled = true };
        Assert.True(s.WordWrapEnabled);
    }

    [Fact]
    public void InitProperty_VimMode_Roundtrips()
    {
        var s = new AppSettings { VimModeEnabled = true };
        Assert.True(s.VimModeEnabled);
    }

    [Fact]
    public void Record_Equality_BySameValues()
    {
        var a = new AppSettings { ThemeName = "Light", TabSize = 2 };
        var b = new AppSettings { ThemeName = "Light", TabSize = 2 };
        Assert.Equal(a, b);
    }

    [Fact]
    public void Record_With_CreatesModifiedCopy()
    {
        var original = new AppSettings { ThemeName = "Dark" };
        var modified = original with { ThemeName = "Light" };
        Assert.Equal("Dark", original.ThemeName);
        Assert.Equal("Light", modified.ThemeName);
    }
}
