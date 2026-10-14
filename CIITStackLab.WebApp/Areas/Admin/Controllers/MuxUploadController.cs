using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
[Route("Admin/Mux")]
public sealed class MuxUploadController : Controller
{
    private readonly IMuxVideoService _muxVideoService;

    public MuxUploadController(IMuxVideoService muxVideoService)
    {
        _muxVideoService = muxVideoService;
    }

    [HttpGet("VideoUploadStatus")]
    public async Task<IActionResult> VideoUploadStatus(string? uploadId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(uploadId))
            return BadRequest(new { message = "A Mux upload ID is required." });

        try
        {
            var status = await _muxVideoService.GetDirectUploadStatusAsync(uploadId, cancellationToken);
            return Json(status);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
