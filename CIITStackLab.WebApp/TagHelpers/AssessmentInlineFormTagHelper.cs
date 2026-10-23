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
        script.Attributes["src"] = "/js/student/assessment-inline.js?v=3";
        script.Attributes["defer"] = "defer";
        output.PostElement.AppendHtml(script);

        var orderingScript = new TagBuilder("script");
        orderingScript.InnerHtml.AppendHtml(@"
(function () {
    const form = document.currentScript?.previousElementSibling?.previousElementSibling;
    if (!form || !form.matches('form[data-ciit-assessment-inline]')) {
        return;
    }

    const questions = Array.from(form.querySelectorAll(':scope > .ciit-learning-question'));
    questions
        .sort((left, right) => {
            const leftId = Number(left.querySelector(""input[name$='.QuestionId']"")?.value);
            const rightId = Number(right.querySelector(""input[name$='.QuestionId']"")?.value);
            return (Number.isFinite(leftId) ? leftId : Number.MAX_SAFE_INTEGER)
                - (Number.isFinite(rightId) ? rightId : Number.MAX_SAFE_INTEGER);
        })
        .forEach(question => form.appendChild(question));
})();");
        output.PostElement.AppendHtml(orderingScript);
    }
}
