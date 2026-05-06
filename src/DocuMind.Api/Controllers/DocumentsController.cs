using DocuMind.Api.Documents;
using Microsoft.AspNetCore.Mvc;

namespace DocuMind.Api.Controllers;

[ApiController]
[Route("v1/documents")]
public sealed class DocumentsController(UploadDocumentHandler handler) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<UploadDocumentResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> UploadAsync(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(file, cancellationToken);
        if (result.IsSuccess)
        {
            var response = result.Document!;
            return CreatedAtRoute(
                routeName: "GetDocumentById",
                routeValues: new { id = response.Id },
                value: response);
        }

        var problemDetails = new ProblemDetails
        {
            Title = "Document upload failed.",
            Detail = result.ErrorMessage,
            Status = result.StatusCode
        };
        problemDetails.Extensions["code"] = result.ErrorCode;

        return StatusCode(result.StatusCode!.Value, problemDetails);
    }

    [HttpGet("{id:guid}", Name = "GetDocumentById")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult GetByIdPlaceholder(Guid id)
    {
        return StatusCode(
            StatusCodes.Status501NotImplemented,
            new ProblemDetails
            {
                Title = "Document retrieval not implemented yet.",
                Detail = $"The GET /v1/documents/{id} endpoint is planned for T07."
            });
    }
}
