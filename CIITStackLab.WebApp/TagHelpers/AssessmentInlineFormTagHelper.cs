using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace CIITStackLab.WebApp.TagHelpers;

[HtmlTargetElement("form", Attributes = "asp-action")]
public sealed class AssessmentInlineFormTagHelper : TagHelper
{
    [HtmlAttributeName("asp-action")]
    public string? Action { get; set; }

    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!string.Equals(Action, "SubmitAssessment", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        output.Attributes.SetAttribute("data-ciit-assessment-inline", "true");

        var script = new TagBuilder("script");
        script.Attributes["src"] = "/js/student/assessment-inline.js?v=1";
        script.Attributes["defer"] = "defer";
        output.PostElement.AppendHtml(script);
    }
}
