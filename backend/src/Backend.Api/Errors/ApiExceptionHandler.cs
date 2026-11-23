using Backend.Api.Contracts.Responses;
using Backend.Application.Common.Exceptions;
using FluentValidation;
using Mapster;
using Microsoft.AspNetCore.Diagnostics;

namespace Backend.Api.Errors;

internal sealed class ApiExceptionHandler : IExceptionHandler {
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    ) {
        var result = exception switch {
            InvalidCredentialsException => Results.Problem(
                title: exception.Message,
                statusCode: StatusCodes.Status401Unauthorized),

            AccessDeniedException => Results.Problem(
                title: exception.Message,
                statusCode: StatusCodes.Status403Forbidden),

            EmailAlreadyExistsException => Results.Problem(
                title: exception.Message,
                statusCode: StatusCodes.Status409Conflict),

            EntityNotFoundException => Results.Problem(
                title: exception.Message,
                statusCode: StatusCodes.Status404NotFound),

            OperationConflictException => Results.Problem(
                title: exception.Message,
                statusCode: StatusCodes.Status409Conflict),

            PaymentPriceChangedException payment => Results.Problem(
                title: payment.Message,
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["quote"] = payment.Quote.Adapt<GroupPaymentQuoteResponse>() }),

            ValidationException validation => Results.ValidationProblem(
                validation.Errors.GroupBy(error => error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.ErrorCode).ToArray()),
                title: "VALIDATION_FAILED"),

            _ => null
        };

        if (result is null) {
            return false;
        }

        await result.ExecuteAsync(httpContext);

        return true;
    }
}
