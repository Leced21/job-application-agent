using System.Net;
using System.Security.Claims;
using JobApplicationAgent.Documents.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobApplicationAgent.Documents.Api;

[Authorize]
[ApiController]
[Route("api/v1/documents/exports")]
public sealed class DocumentExportsController(DocumentExportService service) : ControllerBase
{
    private Guid Owner => DocumentsOwner.From(User);
    [HttpPost("/api/v1/documents/drafts/{draftId:guid}/exports")]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("document-export")]
    public async Task<IActionResult> Create(Guid draftId, CreateDocumentExportCommand command, CancellationToken ct)
    {
        var result = await service.CreateAsync(Owner, draftId, command, ct);
        return result is null ? NotFound() : result.Created ? CreatedAtAction(nameof(Get), new { id = result.Export.Id }, result) : Ok(result);
    }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await service.GetPageAsync(Owner, page, pageSize, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    { var value = await service.GetMetadataAsync(Owner, id, ct); return value is null ? NotFound() : Ok(value); }
    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var result = await service.GetFileAsync(Owner, id, ct);
        if (result is null) return NotFound();
        HashHeaders(result.File.FileSha256, result.File.CompositionSha256);
        return File(result.File.Content, result.File.MediaType, result.File.FileName);
    }
    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        var result = await service.GetFileAsync(Owner, id, ct);
        if (result is null) return NotFound();
        Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        HashHeaders(result.File.FileSha256, result.File.CompositionSha256);
        if (result.File.Format == "Pdf")
        {
            Response.Headers.ContentDisposition = $"inline; filename=\"{result.File.FileName}\"";
            return File(result.File.Content, result.File.MediaType);
        }
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'self'; sandbox";
        var encoded = WebUtility.HtmlEncode(result.PlainText);
        return Content("<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\"><title>Prévisualisation du contenu DOCX</title>" +
            "<style>body{margin:2rem auto;max-width:52rem;padding:0 1.5rem;color:#111;font:16px/1.5 sans-serif}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:inherit}</style>" +
            "</head><body><h1>Prévisualisation du contenu DOCX</h1><p>La pagination et les polices peuvent varier dans votre lecteur Word.</p><pre>" + encoded + "</pre></body></html>", "text/html; charset=utf-8");
    }
    private void HashHeaders(string file, string composition)
    { Response.Headers["X-File-SHA256"] = file; Response.Headers["X-Composition-SHA256"] = composition; }
}
