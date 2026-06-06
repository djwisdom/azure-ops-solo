using System;
using MyCrownJewelApp.Pfpad;
using Xunit;

namespace MyCrownJewelApp.Tests;

public class FontServiceTests
{
    // ── Defaults ────────────────────────────────────────────────────────────

    [Fact] public void NewInstance_HasDefaultFontName()
        => Assert.Equal(FontService.DefaultFontName, new FontService().FontName);

    [Fact] public void NewInstance_HasDefaultFontSize()
        => Assert.Equal(FontService.DefaultFontSize, new FontService().FontSize);

    // ── LoadFrom ────────────────────────────────────────────────────────────

    [Fact] public void LoadFrom_ValidSettings_UpdatesNameAndSize()
    {
        var svc = new FontService();
        svc.LoadFrom(new AppSettings { FontName = "Courier New", FontSize = 14f });
        Assert.Equal("Courier New", svc.FontName);
        Assert.Equal(14f, svc.FontSize);
    }

    [Fact] public void LoadFrom_EmptyFontName_KeepsPreviousValues()
    {
        var svc = new FontService();
        svc.LoadFrom(new AppSettings { FontName = string.Empty, FontSize = 14f });
        Assert.Equal(FontService.DefaultFontName, svc.FontName);
        Assert.Equal(FontService.DefaultFontSize, svc.FontSize);
    }

    [Fact] public void LoadFrom_SizeTooSmall_KeepsPreviousValues()
    {
        var svc = new FontService();
        svc.LoadFrom(new AppSettings { FontName = "Arial", FontSize = 2f });
        Assert.Equal(FontService.DefaultFontName, svc.FontName);
        Assert.Equal(FontService.DefaultFontSize, svc.FontSize);
    }

    [Fact] public void LoadFrom_SizeTooLarge_KeepsPreviousValues()
    {
        var svc = new FontService();
        svc.LoadFrom(new AppSettings { FontName = "Arial", FontSize = 200f });
        Assert.Equal(FontService.DefaultFontName, svc.FontName);
        Assert.Equal(FontService.DefaultFontSize, svc.FontSize);
    }

    [Fact] public void LoadFrom_BoundaryMinSize_Accepted()
    {
        var svc = new FontService();
        svc.LoadFrom(new AppSettings { FontName = "Arial", FontSize = FontService.MinFontSize });
        Assert.Equal(FontService.MinFontSize, svc.FontSize);
    }

    [Fact] public void LoadFrom_BoundaryMaxSize_Accepted()
    {
        var svc = new FontService();
        svc.LoadFrom(new AppSettings { FontName = "Arial", FontSize = FontService.MaxFontSize });
        Assert.Equal(FontService.MaxFontSize, svc.FontSize);
    }

    // ── SetFontSize ─────────────────────────────────────────────────────────

    [Fact] public void SetFontSize_ChangedValue_ReturnsTrue()
    {
        var svc = new FontService();
        Assert.True(svc.SetFontSize(20f));
        Assert.Equal(20f, svc.FontSize);
    }

    [Fact] public void SetFontSize_SameValue_ReturnsFalse()
    {
        var svc = new FontService();
        Assert.False(svc.SetFontSize(FontService.DefaultFontSize));
    }

    [Fact] public void SetFontSize_BelowMin_ClampsToMin()
    {
        var svc = new FontService();
        svc.SetFontSize(1f);
        Assert.Equal(FontService.MinFontSize, svc.FontSize);
    }

    [Fact] public void SetFontSize_AboveMax_ClampsToMax()
    {
        var svc = new FontService();
        svc.SetFontSize(999f);
        Assert.Equal(FontService.MaxFontSize, svc.FontSize);
    }
}
