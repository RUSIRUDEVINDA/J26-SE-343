namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// External document intelligence service port representing OCR, classification, and extraction capabilities.
/// Adapters may invoke Python/FastAPI pipelines (e.g. OpenCV, Tesseract, Qwen), but the Application
/// boundary remains decoupled from model and framework implementation details.
/// </summary>
public interface IDocumentIntelligenceService
{
    Task<DocumentIntelligenceResult> AnalyzeDocumentAsync(
        DocumentIntelligenceRequest request,
        CancellationToken cancellationToken = default);
}
