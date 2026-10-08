using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.Validators;

public sealed class CreateLandParcelCommandValidator : IRequestValidator<CreateLandParcelCommand>
{
    public ValidationResult Validate(CreateLandParcelCommand request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.CadastralNumber))
        {
            errors.Add("Cadastral number is required.");
        }

        if (request.AreaValue <= 0)
        {
            errors.Add("Area value must be greater than zero.");
        }

        if (!Enum.IsDefined(request.AreaUnit))
        {
            errors.Add("Area unit must be SquareMeters, Hectares, or Acres.");
        }

        if (string.IsNullOrWhiteSpace(request.Province))
        {
            errors.Add("Province is required.");
        }

        if (string.IsNullOrWhiteSpace(request.District))
        {
            errors.Add("District is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DivisionalSecretariat))
        {
            errors.Add("Divisional secretariat is required.");
        }

        if (request.CentroidLatitude is < -90 or > 90)
        {
            errors.Add("Centroid latitude must be between -90 and 90.");
        }

        if (request.CentroidLongitude is < -180 or > 180)
        {
            errors.Add("Centroid longitude must be between -180 and 180.");
        }

        if (request.BoundaryPolygon is not null && GeoJsonGeometryMapper.ToGeoBoundary(request.BoundaryPolygon) is null)
        {
            errors.Add("Boundary polygon must include at least three valid [longitude, latitude] positions.");
        }

        errors.AddRange(ValidateSpatialConstraints(request.SpatialConstraints));
        errors.AddRange(ValidateEnvironmentalRestrictions(request.EnvironmentalRestrictions));
        errors.AddRange(ValidateInfrastructureFeatures(request.InfrastructureFeatures));
        errors.AddRange(ValidateRegulatoryReferences(request.RegulatoryReferences));

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    private static IEnumerable<string> ValidateSpatialConstraints(
        IReadOnlyList<SpatialConstraintInputDto>? constraints)
    {
        if (constraints is null)
        {
            yield break;
        }

        for (var index = 0; index < constraints.Count; index++)
        {
            var item = constraints[index];
            if (string.IsNullOrWhiteSpace(item.Description))
            {
                yield return $"Spatial constraint at index {index} requires a description.";
            }

            if (!Enum.IsDefined(item.Type))
            {
                yield return $"Spatial constraint at index {index} has an invalid type.";
            }

            if (!Enum.IsDefined(item.Severity))
            {
                yield return $"Spatial constraint at index {index} has an invalid severity.";
            }

            if (item.Geometry is not null && GeoJsonGeometryMapper.ToGeoBoundary(item.Geometry) is null)
            {
                yield return $"Spatial constraint at index {index} has invalid geometry.";
            }
        }
    }

    private static IEnumerable<string> ValidateEnvironmentalRestrictions(
        IReadOnlyList<EnvironmentalRestrictionInputDto>? restrictions)
    {
        if (restrictions is null)
        {
            yield break;
        }

        for (var index = 0; index < restrictions.Count; index++)
        {
            var item = restrictions[index];
            if (string.IsNullOrWhiteSpace(item.Description))
            {
                yield return $"Environmental restriction at index {index} requires a description.";
            }
        }
    }

    private static IEnumerable<string> ValidateInfrastructureFeatures(
        IReadOnlyList<InfrastructureFeatureInputDto>? features)
    {
        if (features is null)
        {
            yield break;
        }

        for (var index = 0; index < features.Count; index++)
        {
            var item = features[index];
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                yield return $"Infrastructure feature at index {index} requires a name.";
            }

            if (item.DistanceMeters is < 0)
            {
                yield return $"Infrastructure feature at index {index} cannot have a negative distance.";
            }
        }
    }

    private static IEnumerable<string> ValidateRegulatoryReferences(
        IReadOnlyList<RegulatoryReferenceInputDto>? references)
    {
        if (references is null)
        {
            yield break;
        }

        for (var index = 0; index < references.Count; index++)
        {
            var item = references[index];
            if (string.IsNullOrWhiteSpace(item.GazetteNumber))
            {
                yield return $"Regulatory reference at index {index} requires a gazette number.";
            }

            if (string.IsNullOrWhiteSpace(item.Title))
            {
                yield return $"Regulatory reference at index {index} requires a title.";
            }
        }
    }
}
