using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Presentation;
using StateLandGovernance.LandIntelligence.Presentation.Controllers;
using StateLandGovernance.LandIntelligence.Presentation.Models.Responses;

namespace StateLandGovernance.UnitTests.LandIntelligence.Presentation;

public sealed class LandIntelligenceExternalApiContractTests
{
    private static readonly Type[] ForbiddenResponseTypes =
    [
        typeof(LandParcelEntity),
        typeof(Geometry),
        typeof(Point),
        typeof(Polygon)
    ];

    [Theory]
    [InlineData(typeof(LandParcelsController))]
    [InlineData(typeof(LandSearchController))]
    [InlineData(typeof(LandRecommendationsController))]
    public void External_controllers_expose_only_read_operations(Type controllerType)
    {
        var mutatingMethods = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<HttpPutAttribute>() is not null
                || method.GetCustomAttribute<HttpDeleteAttribute>() is not null
                || method.GetCustomAttribute<HttpPatchAttribute>() is not null)
            .Select(method => method.Name)
            .ToList();

        Assert.Empty(mutatingMethods);
    }

    [Fact]
    public void Internal_parcel_controller_exposes_write_operations()
    {
        var writeMethods = typeof(LandParcelsInternalController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<HttpPostAttribute>() is not null
                || method.GetCustomAttribute<HttpPutAttribute>() is not null
                || method.GetCustomAttribute<HttpDeleteAttribute>() is not null)
            .Select(method => method.Name)
            .ToList();

        Assert.Contains("CreateParcelAsync", writeMethods);
        Assert.Contains("UpdateParcelAsync", writeMethods);
        Assert.Contains("DeleteParcelAsync", writeMethods);
    }

    [Theory]
    [InlineData(typeof(LandParcelsController))]
    [InlineData(typeof(LandSearchController))]
    [InlineData(typeof(LandRecommendationsController))]
    public void External_controller_actions_return_presentation_response_dtos(Type controllerType)
    {
        foreach (var method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (method.GetCustomAttribute<HttpGetAttribute>() is null
                && method.GetCustomAttribute<HttpPostAttribute>() is null)
            {
                continue;
            }

            var responseType = UnwrapActionResultType(method.ReturnType);
            AssertResponseContract(responseType, method.Name);
        }
    }

    [Fact]
    public void Response_dtos_do_not_reference_persistence_or_postgis_geometry_types()
    {
        var responseAssembly = typeof(LandParcelResponse).Assembly;

        foreach (var type in responseAssembly
                     .GetTypes()
                     .Where(type => type.Namespace?.EndsWith(".Models.Responses", StringComparison.Ordinal) == true))
        {
            Assert.DoesNotContain(
                ForbiddenResponseTypes,
                forbidden => TypeReferences(type, forbidden));
        }
    }

    private static void AssertResponseContract(Type responseType, string methodName)
    {
        var contractTypes = ExtractContractTypes(responseType);

        Assert.All(
            contractTypes,
            contractType => Assert.True(
                contractType.Namespace?.StartsWith(
                    "StateLandGovernance.LandIntelligence.Presentation.Models.Responses",
                    StringComparison.Ordinal) == true,
                $"Action {methodName} must return Presentation response DTOs, not {contractType.FullName}."));

        Assert.All(
            contractTypes,
            contractType => Assert.DoesNotContain(
                ForbiddenResponseTypes,
                forbidden => TypeReferences(contractType, forbidden)));
    }

    private static Type UnwrapActionResultType(Type returnType)
    {
        if (!returnType.IsGenericType)
        {
            return returnType;
        }

        var genericDefinition = returnType.GetGenericTypeDefinition();
        if (genericDefinition == typeof(Task<>))
        {
            return UnwrapActionResultType(returnType.GetGenericArguments()[0]);
        }

        if (genericDefinition == typeof(ActionResult<>))
        {
            return returnType.GetGenericArguments()[0];
        }

        return returnType;
    }

    private static IEnumerable<Type> ExtractContractTypes(Type responseType)
    {
        if (TryGetCollectionElementType(responseType, out var elementType))
        {
            return ExtractContractTypes(elementType);
        }

        if (responseType.IsGenericType)
        {
            return responseType.GetGenericArguments().SelectMany(ExtractContractTypes);
        }

        return [responseType];
    }

    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(IReadOnlyList<>)
                || definition == typeof(IEnumerable<>)
                || definition == typeof(IList<>)
                || definition == typeof(List<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = type;
        return false;
    }

    private static bool TypeReferences(Type type, Type forbidden) =>
        TypeReferences(type, forbidden, []);

    private static bool TypeReferences(Type type, Type forbidden, HashSet<Type> visited)
    {
        if (type == forbidden)
        {
            return true;
        }

        if (!visited.Add(type))
        {
            return false;
        }

        if (type.IsGenericType)
        {
            return type.GetGenericArguments().Any(argument => TypeReferences(argument, forbidden, visited));
        }

        if (ShouldStopTypeTraversal(type))
        {
            return false;
        }

        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => TypeReferences(property.PropertyType, forbidden, visited));
    }

    private static bool ShouldStopTypeTraversal(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(Guid)
        || type == typeof(TimeSpan)
        || type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true;
}
