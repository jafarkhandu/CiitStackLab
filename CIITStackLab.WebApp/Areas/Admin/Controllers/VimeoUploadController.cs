using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
[Route("Admin/Vimeo")]
public sealed class VimeoUploadController : Controller
{
    private readonly IVimeoVideoService _vimeoVideoService;

    public VimeoUploadController(IVimeoVideoService vimeoVideoService)
    {
        _vimeoVideoService = vimeoVideoService;
    }

    [HttpGet("VideoUploadStatus")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> VideoUploadStatus(
        string? videoId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(videoId) || !videoId.All(char.IsDigit))
            return BadRequest(new { message = "A valid Vimeo video ID is required." });

        try
        {
            var status = await _vimeoVideoService.GetVideoStatusAsync(
                videoId,
                cancellationToken);

            return Json(status);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
