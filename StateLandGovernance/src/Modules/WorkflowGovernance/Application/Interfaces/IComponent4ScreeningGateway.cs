namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Neutral integration port for dispatching screening requests to Component 4 (Institutional &amp; Regulatory Screening).
/// Component 3 sends verified facts bound to an authoritative snapshot.
/// Technical delivery, transport, and retry mechanisms belong to future Infrastructure adapters.
/// Technical delivery failure never mutates business screening state to Blocked or Cleared.
/// </summary>
public interface IComponent4ScreeningGateway
{
    Task DispatchScreeningRequestAsync(
        Component4ScreeningRequest request,
        CancellationToken cancellationToken = default);
}
