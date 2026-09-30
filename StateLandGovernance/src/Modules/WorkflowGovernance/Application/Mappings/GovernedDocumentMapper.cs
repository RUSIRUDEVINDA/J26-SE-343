namespace StateLandGovernance.WorkflowGovernance.Application.Mappings;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public static class GovernedDocumentMapper
{
    public static GovernedDocumentDto ToDto(GovernedDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var versions = document.Versions
            .OrderBy(v => v.VersionNumber)
            .Select(ToVersionDto)
            .ToList();

        var activeVersionDto = versions.First(v => v.Id == document.ActiveVersionId.Value);

        return new GovernedDocumentDto(
            Id: document.Id.Value,
            LeaseCaseId: document.LeaseCaseId.Value,
            LogicalCategory: document.LogicalCategory,
            ActiveVersionId: document.ActiveVersionId.Value,
            Revision: document.Revision,
            VersionCount: document.Versions.Count,
            ActiveVersion: activeVersionDto,
            Versions: versions
        );
    }

    public static DocumentVersionDto ToVersionDto(DocumentVersion version)
    {
        if (version == null)
        {
            throw new ArgumentNullException(nameof(version));
        }

        return new DocumentVersionDto(
            Id: version.Id.Value,
            VersionNumber: version.VersionNumber,
            PredecessorVersionId: version.PredecessorVersionId?.Value,
            ChecksumAlgorithm: version.Checksum.Algorithm,
            ChecksumValue: version.Checksum.Value,
            OriginalFileName: version.OriginalFileName,
            MediaType: version.MediaType,
            FileSizeInBytes: version.FileSizeInBytes,
            SubmittedByActorId: version.SubmittedByActorId,
            SubmittedAt: version.SubmittedAt
        );
    }
}
