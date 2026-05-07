using DocuMind.Api.Documents.Commands.UploadDocument;
using DocuMind.Api.Documents.Queries.GetDocumentById;
using Microsoft.AspNetCore.Mvc;

namespace DocuMind.Api.Controllers;

[ApiController]
[Route("v1/documents")]
public sealed class DocumentsController(
    UploadDocumentCommandHandler uploadHandler,
    GetDocumentByIdQueryHandler getDocumentByIdHandler) : ControllerBase
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
        var result = await uploadHandler.HandleAsync(file, cancellationToken);
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
    [ProducesResponseType<GetDocumentByIdResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await getDocumentByIdHandler.HandleAsync(id, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Document);
        }

        var problemDetails = new ProblemDetails
        {
            Title = "Document retrieval failed.",
            Detail = result.ErrorMessage,
            Status = result.StatusCode
        };
        problemDetails.Extensions["code"] = result.ErrorCode;

        return StatusCode(result.StatusCode!.Value, problemDetails);
    }
}
