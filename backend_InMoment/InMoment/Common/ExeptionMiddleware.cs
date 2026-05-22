using InMoment.Domain.Common;
using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Common;

public sealed class ExceptionMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(ILogger<ExceptionMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain exception occurred. TraceId: {TraceId}", context.TraceIdentifier);

            await WriteProblem(
                context,
                title: ex.Message,
                code: StatusFromDomain(ex),
                traceId: context.TraceIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);

            await WriteProblem(
                context,
                title: "Произошла внутренняя ошибка сервера.",
                code: HttpStatusCode.InternalServerError,
                traceId: context.TraceIdentifier);
        }
    }

    private static HttpStatusCode StatusFromDomain(DomainException ex) => ex switch
    {
        NotFoundException => HttpStatusCode.NotFound,
        ForbiddenException => HttpStatusCode.Forbidden,
        Domain.Common.ValidationException => HttpStatusCode.BadRequest,
        _ => HttpStatusCode.BadRequest
    };

    private static async Task WriteProblem(
        HttpContext ctx,
        string title,
        HttpStatusCode code,
        string traceId)
    {
        if (ctx.Response.HasStarted)
            return;

        ctx.Response.StatusCode = (int)code;
        ctx.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Title = title,
            Status = (int)code
        };

        problem.Extensions["traceId"] = traceId;

        await ctx.Response.WriteAsJsonAsync(problem);
    }
}