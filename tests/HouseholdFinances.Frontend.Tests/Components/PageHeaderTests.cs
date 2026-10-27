using Bunit;
using HouseholdFinances.Frontend.Components.Shared;
using MudBlazor.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit component tests for the shared PageHeader: every page renders a single heading, and the
/// optional subtitle, inside the shared <c>page-header</c> element so the header hierarchy is
/// consistent sitewide. The tests prove the rendered structure; they do not prove how the page
/// looks in a browser.
/// </summary>
public class PageHeaderTests : BunitContext
{
    public PageHeaderTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    [Fact]
    public void PageHeader_RendersTitleAndSubtitleInTheSharedHeaderElement()
    {
        var cut = Render<PageHeader>(parameters => parameters
            .Add(header => header.Title, "Bills")
            .Add(header => header.Subtitle, "Track the bills paid by your household."));

        var header = cut.Find("header.page-header");
        var heading = Assert.Single(cut.FindAll("h4"));

        Assert.Equal("Bills", heading.TextContent.Trim());
        Assert.Contains("Track the bills paid by your household.", header.TextContent);
    }

    [Fact]
    public void PageHeader_WithoutSubtitle_RendersOnlyTheHeading()
    {
        var cut = Render<PageHeader>(parameters => parameters
            .Add(header => header.Title, "Profile"));

        Assert.Single(cut.FindAll("h4"));
        Assert.Empty(cut.FindAll(".page-header-subtitle"));
    }
}
