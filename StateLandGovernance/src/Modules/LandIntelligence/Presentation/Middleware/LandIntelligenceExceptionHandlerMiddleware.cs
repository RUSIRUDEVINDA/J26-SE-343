using System.Text.Json;

using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.Logging;

using StateLandGovernance.LandIntelligence.Application.Interfaces;

using StateLandGovernance.LandIntelligence.Domain.Exceptions;

using StateLandGovernance.LandIntelligence.Presentation.Models;



namespace StateLandGovernance.LandIntelligence.Presentation.Middleware;



public sealed class LandIntelligenceExceptionHandlerMiddleware

{

    private static readonly JsonSerializerOptions SerializerOptions = new()

    {

        PropertyNamingPolicy = JsonNamingPolicy.CamelCase

    };



    private readonly RequestDelegate _next;

    private readonly ILogger<LandIntelligenceExceptionHandlerMiddleware> _logger;



    public LandIntelligenceExceptionHandlerMiddleware(

        RequestDelegate next,

        ILogger<LandIntelligenceExceptionHandlerMiddleware> logger)

    {

        _next = next;

        _logger = logger;

    }



    public async Task InvokeAsync(HttpContext context)

    {

        try

        {

            await _next(context);

        }

        catch (Exception exception)

        {

            _logger.LogError(exception, "Land Intelligence request failed for {Method} {Path}.", context.Request.Method, context.Request.Path);

            await WriteErrorResponseAsync(context, exception);

        }

    }



    private static async Task WriteErrorResponseAsync(HttpContext context, Exception exception)

    {

        var (statusCode, title, detail, errors) = MapException(exception);



        context.Response.StatusCode = statusCode;

        context.Response.ContentType = "application/json";



        var payload = new ApiErrorResponse(statusCode, title, detail, errors);

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));

    }



    private static (int StatusCode, string Title, string Detail, IReadOnlyList<string>? Errors) MapException(

        Exception exception)

    {

        return exception switch

        {

            ValidationException validation => (

                StatusCodes.Status400BadRequest,

                "Validation failed.",

                "One or more validation errors occurred.",

                validation.Errors),



            LandParcelNotFoundException notFound => (

                StatusCodes.Status404NotFound,

                "Land parcel not found.",

                notFound.Message,

                null),



            LandRecommendationNotFoundException recommendationNotFound => (

                StatusCodes.Status404NotFound,

                "Land recommendation not found.",

                recommendationNotFound.Message,

                null),



            LandIntelligenceDomainException domain => (

                StatusCodes.Status422UnprocessableEntity,

                "Domain rule violation.",

                domain.Message,

                null),



            ServiceConfigurationException => (

                StatusCodes.Status503ServiceUnavailable,

                "Service unavailable.",

                "A required Land Intelligence dependency is not configured.",

                null),



            _ => (

                StatusCodes.Status500InternalServerError,

                "An unexpected error occurred.",

                "An internal server error occurred while processing the request.",

                null)

        };

    }

}


