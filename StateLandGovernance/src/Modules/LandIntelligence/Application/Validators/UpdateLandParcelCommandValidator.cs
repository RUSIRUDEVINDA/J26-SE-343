using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;

namespace StateLandGovernance.LandIntelligence.Application.Validators;

public sealed class UpdateLandParcelCommandValidator : IRequestValidator<UpdateLandParcelCommand>
{
    public ValidationResult Validate(UpdateLandParcelCommand request)
    {
        var errors = new List<string>();

        if (request.LandParcelId == Guid.Empty)
        {
            errors.Add("Land parcel identifier is required.");
        }

        if (request.CurrentUseType is null
            && request.CurrentUseDescription is null
            && request.Characteristics is null
            && request.CentroidLatitude is null
            && request.CentroidLongitude is null
            && request.BoundaryPolygon is null
            && request.SpatialConstraints is null
            && request.EnvironmentalRestrictions is null
            && request.InfrastructureFeatures is null
            && request.RegulatoryReferences is null)
        {
            errors.Add("At least one updatable field must be provided.");
        }

        if (request.CentroidLatitude is not null ^ request.CentroidLongitude is not null)
        {
            errors.Add("Centroid latitude and longitude must be provided together.");
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

        if (request.SpatialConstraints is not null)
        {
            foreach (var item in request.SpatialConstraints.Where(item => string.IsNullOrWhiteSpace(item.Description)))
            {
                errors.Add("Each spatial constraint requires a description.");
            }
        }

        if (request.InfrastructureFeatures is not null)
        {
            foreach (var item in request.InfrastructureFeatures.Where(item => string.IsNullOrWhiteSpace(item.Name)))
            {
                errors.Add("Each infrastructure feature requires a name.");
            }
        }

        if (request.RegulatoryReferences is not null)
        {
            foreach (var item in request.RegulatoryReferences.Where(item => string.IsNullOrWhiteSpace(item.GazetteNumber)))
            {
                errors.Add("Each regulatory reference requires a gazette number.");
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
