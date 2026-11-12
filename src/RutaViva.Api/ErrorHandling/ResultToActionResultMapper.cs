using Microsoft.AspNetCore.Mvc;
using RutaViva.Domain.Common;

namespace RutaViva.Api.ErrorHandling;

/// <summary>Translates business results to HTTP: Validation → 400, NotFound → 404, Conflict → 409, as ProblemDetails (RFC 9457).</summary>
public static class ResultToActionResultMapper
{
    public static ActionResult<T> ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        return result.IsSuccess ? controller.Ok(result.Value) : controller.ToProblem(result.Error);
    }

    public static ObjectResult ToProblem(this ControllerBase controller, Error error)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(error);

        var status = StatusCodeFor(error.Type);
        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(
            controller.HttpContext,
            statusCode: status,
            title: TitleFor(error.Type),
            detail: error.Message);
        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }

    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "La solicitud no es válida",
        ErrorType.NotFound => "Recurso no encontrado",
        ErrorType.Conflict => "La operación no es posible en el estado actual",
        _ => "Error inesperado",
    };
}
