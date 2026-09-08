using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Attendance;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Common;

namespace Ts.Api.Api;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            ResourceNotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            PreconditionFailedException => StatusCodes.Status412PreconditionFailed,
            DbUpdateException => StatusCodes.Status409Conflict,
            DomainException => StatusCodes.Status422UnprocessableEntity,
            WhatsAppProviderException => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status500InternalServerError,
        };
        var errorId = Guid.NewGuid().ToString("N");
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Erro não tratado {ErrorId} na correlação {CorrelationId}", errorId, httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning("Requisição rejeitada com {StatusCode}, erro {ErrorId} e correlação {CorrelationId}: {ExceptionType}",
                statusCode, errorId, httpContext.TraceIdentifier, exception.GetType().Name);
        }

        httpContext.Response.StatusCode = statusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode switch
                {
                    StatusCodes.Status404NotFound => "Recurso não encontrado",
                    StatusCodes.Status409Conflict => "Conflito de estado",
                    StatusCodes.Status412PreconditionFailed => "Versão desatualizada",
                    StatusCodes.Status422UnprocessableEntity => "Regra de negócio inválida",
                    StatusCodes.Status502BadGateway => "Integração externa indisponível",
                    _ => "Erro interno",
                },
                Detail = statusCode == StatusCodes.Status500InternalServerError
                    ? "Ocorreu um erro inesperado. Use os identificadores de erro e correlação ao solicitar suporte."
                    : exception is DbUpdateException
                    ? "A operação conflita com o estado persistido. Recarregue os dados e tente novamente."
                    : exception.Message,
                Extensions =
                {
                    ["errorId"] = errorId,
                    ["correlationId"] = httpContext.TraceIdentifier,
                },
            },
        });
    }
}
