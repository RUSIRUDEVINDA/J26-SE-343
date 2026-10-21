using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace StateLandGovernance.Api.Controllers;

// Used only by the explicit Component 2 development profile.
public sealed class FinancialControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        for (var index = feature.Controllers.Count - 1; index >= 0; index--)
            if (feature.Controllers[index].AsType() != typeof(FinancialDocumentsController) &&
                feature.Controllers[index].AsType() != typeof(FinancialAssessmentController) &&
                feature.Controllers[index].AsType() != typeof(LeaseProposalsController))
                feature.Controllers.RemoveAt(index);
    }
}
