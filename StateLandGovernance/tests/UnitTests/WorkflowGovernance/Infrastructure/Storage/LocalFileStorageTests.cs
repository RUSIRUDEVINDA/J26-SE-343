namespace StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure.Storage;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Storage;
using Xunit;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly LocalFileDocumentContentStore _documentStore;
    private readonly LocalFileAnalysisArtifactStore _artifactStore;

    public LocalFileStorageTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "slg-storage-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        var options = Options.Create(new LocalFileStorageOptions
        {
            RootPath = _tempRoot
        });

        _documentStore = new LocalFileDocumentContentStore(options, NullLogger<LocalFileDocumentContentStore>.Instance);
        _artifactStore = new LocalFileAnalysisArtifactStore(options, NullLogger<LocalFileAnalysisArtifactStore>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup in test temp
        }
    }

    [Fact]
    public async Task DocumentStore_WriteAsync_And_ReadAsync_PreservesExactBytesAndSha256()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("Test lease deed content for governance verification.");
        var expectedSha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        using var stream = new MemoryStream(content);
        var docId = Guid.NewGuid();
        var verId = Guid.NewGuid();

        // Act
        var receipt = await _documentStore.WriteAsync(
            stream,
            originalFileName: "deed.pdf",
            mediaType: "application/pdf",
            documentId: docId,
            versionId: verId);

        // Assert receipt
        Assert.NotNull(receipt);
        Assert.Equal(expectedSha, receipt.ChecksumValue);
        Assert.Equal(content.Length, receipt.FileSizeInBytes);
        Assert.Contains(docId.ToString("D"), receipt.ContentReference);
        Assert.Contains(verId.ToString("D"), receipt.ContentReference);
        Assert.Contains("source.bin", receipt.ContentReference);

        // Act - Read
        using var readStream = await _documentStore.ReadContentAsync(
            new StateLandGovernance.WorkflowGovernance.Domain.Documents.GovernedDocumentId(docId),
            new StateLandGovernance.WorkflowGovernance.Domain.Documents.DocumentVersionId(verId),
            receipt.ContentReference);
        Assert.NotNull(readStream);
        using var ms = new MemoryStream();
        await readStream.CopyToAsync(ms);
        var readBytes = ms.ToArray();

        // Assert read bytes
        Assert.Equal(content, readBytes);
    }

    [Fact]
    public async Task DocumentStore_ReadContentAsync_NonExistentFile_ReturnsNull()
    {
        var result = await _documentStore.ReadContentAsync(
            new StateLandGovernance.WorkflowGovernance.Domain.Documents.GovernedDocumentId(Guid.NewGuid()),
            new StateLandGovernance.WorkflowGovernance.Domain.Documents.DocumentVersionId(Guid.NewGuid()),
            "non/existent/path/doc.pdf");
        Assert.Null(result);
    }

    [Theory]
    [InlineData("../sneaky.pdf")]
    [InlineData("..\\sneaky.pdf")]
    [InlineData("folder/../../sneaky.pdf")]
    [InlineData("folder/..\\sneaky.pdf")]
    [InlineData("/root/absolute.pdf")]
    [InlineData("C:/root/absolute.pdf")]
    [InlineData("C:\\root\\absolute.pdf")]
    public async Task DocumentStore_ReadContentAsync_PathTraversal_ThrowsArgumentException(string traversalPath)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _documentStore.ReadContentAsync(
            new StateLandGovernance.WorkflowGovernance.Domain.Documents.GovernedDocumentId(Guid.NewGuid()),
            new StateLandGovernance.WorkflowGovernance.Domain.Documents.DocumentVersionId(Guid.NewGuid()),
            traversalPath));
    }

    [Fact]
    public async Task DocumentStore_WriteAsync_NullOrEmptyParameters_ThrowsException()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<ArgumentNullException>(() => _documentStore.WriteAsync(null!, "a.pdf", "app/pdf"));
        await Assert.ThrowsAsync<ArgumentException>(() => _documentStore.WriteAsync(stream, "", "app/pdf"));
        await Assert.ThrowsAsync<ArgumentException>(() => _documentStore.WriteAsync(stream, "   ", "app/pdf"));
    }

    [Fact]
    public async Task ArtifactStore_WriteAsync_And_ReadAsync_PreservesExactBytesAndSha256()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("{\"page\":1,\"text\":\"Government Gazette Extract\"}");
        var expectedSha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var proposedKey = "artifacts/analysis-123/page-1.json";

        // Act - Write
        var receipt = await _artifactStore.WriteAsync(
            proposedKey: proposedKey,
            contentType: "application/json",
            content: content);

        // Assert receipt
        Assert.NotNull(receipt);
        Assert.Equal(proposedKey, receipt.StorageReference);
        Assert.Equal(expectedSha, receipt.ChecksumValue);
        Assert.Equal(content.Length, receipt.ContentLength);

        // Act - Read
        var readMemory = await _artifactStore.ReadAsync(receipt.StorageReference);
        Assert.True(readMemory.HasValue);
        Assert.Equal(content, readMemory.Value.ToArray());
    }

    [Fact]
    public async Task ArtifactStore_ReadAsync_NonExistentKey_ReturnsNull()
    {
        var result = await _artifactStore.ReadAsync("non/existent/artifact.json");
        Assert.Null(result);
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("..\\outside.json")]
    [InlineData("artifacts/../../outside.json")]
    [InlineData("artifacts/..\\outside.json")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/outside.json")]
    public async Task ArtifactStore_ReadAsync_PathTraversal_ThrowsArgumentException(string traversalKey)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _artifactStore.ReadAsync(traversalKey));
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("..\\outside.json")]
    [InlineData("artifacts/../../outside.json")]
    public async Task ArtifactStore_WriteAsync_PathTraversal_ThrowsArgumentException(string traversalKey)
    {
        var bytes = new byte[] { 1, 2, 3 };
        await Assert.ThrowsAsync<ArgumentException>(() => _artifactStore.WriteAsync(traversalKey, "application/json", bytes));
    }

    [Fact]
    public async Task ArtifactStore_WriteAsync_CleansUpTmpFileOnSuccess()
    {
        var content = Encoding.UTF8.GetBytes("Atomic write test content");
        var proposedKey = "artifacts/atomic/test.txt";

        var receipt = await _artifactStore.WriteAsync(proposedKey, "text/plain", content);
        Assert.NotNull(receipt);

        var targetPath = Path.Combine(_tempRoot, "artifacts", "atomic", "test.txt");
        Assert.True(File.Exists(targetPath));

        // Ensure no leftover .tmp files
        var dir = Path.GetDirectoryName(targetPath)!;
        var tmpFiles = Directory.GetFiles(dir, "*.tmp");
        Assert.Empty(tmpFiles);
    }
}
