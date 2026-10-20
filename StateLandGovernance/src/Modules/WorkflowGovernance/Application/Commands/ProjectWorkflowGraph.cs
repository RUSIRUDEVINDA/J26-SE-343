namespace StateLandGovernance.WorkflowGovernance.Application.Commands;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;

/// <summary>User-specified project topology; selection reasons and spatial coverage are supplied, never inferred.</summary>
public static class ProjectWorkflowGraph
{
    public static string? SourceBindingError(ProjectRoutingContext context, StoredCaseScreening? assessment, bool isDevelopmentMock)
    {
        if (context.IsDevelopmentMock != isDevelopmentMock)
            return "Routing spatial provenance differs from the screening evidence; development mocks cannot become real coverage.";
        try
        {
            using var source = JsonDocument.Parse(assessment?.SourceBindingsJson ?? "null");
            if (source.RootElement.ValueKind != JsonValueKind.Object) return "Routing requires the screening's exact spatial source/version binding.";
            if (isDevelopmentMock)
            {
                var spatial = source.RootElement.GetProperty("spatialProvenance");
                var provinces = spatial.GetProperty("provinces").EnumerateArray().Select(p => p.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (spatial.GetProperty("version").GetString() != context.SpatialVersion || !provinces.SetEquals(context.Provinces))
                    return "Routing province coverage/version differs from the exact development spatial evidence used for screening.";
            }
            else if (source.RootElement.GetProperty("review").GetProperty("version").GetString() != context.SpatialVersion)
                return "Routing province coverage must bind to the exact trusted case/parcel evidence review version used for screening.";
            return null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        { return "Routing requires the screening's complete spatial source/version and province coverage binding."; }
    }

    public static IReadOnlyList<WorkflowStage> Build(RoutingPolicyDto policy, string binding,
        IReadOnlyDictionary<string, string> facts)
    {
        var context = policy.ProjectRouting ?? throw new InvalidOperationException("Project routing context is unavailable.");
        if (string.IsNullOrWhiteSpace(context.SourceReference) || string.IsNullOrWhiteSpace(context.Version) ||
            string.IsNullOrWhiteSpace(context.SpatialVersion) || context.Provinces.Count == 0 ||
            context.Provinces.Any(string.IsNullOrWhiteSpace) || context.Provinces.Distinct(StringComparer.OrdinalIgnoreCase).Count() != context.Provinces.Count)
            throw new InvalidOperationException("Versioned province coverage is incomplete or ambiguous.");
        if (context.CabinetThresholdAcres != 50m)
            throw new InvalidOperationException("This project workflow specifies Cabinet strictly above 50 acres.");
        if (context.SelectedDepartmentReasons.Any(x => string.IsNullOrWhiteSpace(x.Value) || !policy.Institutions.Contains(x.Key)))
            throw new InvalidOperationException("Selected departments need explicit reasons and membership in the configured extensible pool.");
        var hectares = facts.TryGetValue("land_extent", out var extent) && facts.TryGetValue("land_extent_unit", out var unit)
            ? LandExtentNormalizer.TryNormalizeToHectares(extent, unit) : null;
        if (hectares is null or <= 0) throw new InvalidOperationException("Verified exact land extent/unit are required for Cabinet routing.");
        var stages = new List<WorkflowStage>();
        // IDs stay stable across governed department additions; full input binding remains on the plan.
        WorkflowStageId Id(string code) => new(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(binding + ":" + code)).AsSpan(0, 16)));
        WorkflowStage Add(string code, string institution, string cap, IEnumerable<WorkflowStageId> deps, string why, bool final = false)
        {
            var stage = new WorkflowStage(Id(code), new(code), new(institution), final ? WorkflowStageType.FinalDecision : WorkflowStageType.Review,
                cap, new("USER_PROJECT_WORKFLOW"), why, null, final, deps);
            stages.Add(stage); return stage;
        }
        var cabinet = hectares.Value > 50m * 0.40468564224m
            ? Add("CABINET", "CABINET", "CabinetReviewer", Array.Empty<WorkflowStageId>(), "Verified extent is strictly greater than the project threshold of 50 acres.") : null;
        var entry = cabinet == null ? Array.Empty<WorkflowStageId>() : new[] { cabinet.Id };
        var external = context.SelectedDepartmentReasons.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => Add("EXTERNAL_" + x.Key, x.Key, policy.StandardCapability, entry, x.Value)).ToList();
        var ds = Add("DS", "DS", policy.DivisionalCapability, external.Count > 0 ? external.Select(s => s.Id) : entry,
            "Mandatory DS review after the selected external reviews; rejection blocks progression.");
        var regional = context.Provinces.Count == 1 ? "PROVINCIAL_LC" : "INTER_PROVINCIAL_LC";
        var province = Add(regional, regional, "ProvincialReviewer", new[] { ds.Id },
            context.Provinces.Count == 1 ? "Versioned spatial evidence identifies one province." : "Versioned spatial evidence identifies multiple provinces; no border distance inferred.");
        Add("COMMISSIONER", "COMMISSIONER", policy.CommissionerCapability, new[] { province.Id },
            "Commissioner receives the regional outcome including rejection and records an independent explicit decision.", true);
        return stages;
    }
}
