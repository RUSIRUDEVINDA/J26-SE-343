using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.Blockchain;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class HttpBlockchainAuditAnchorServiceTests
{
    private readonly Guid _testRecordId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private readonly string _testHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    [Fact]
    public async Task AnchorRecordAsync_OnHttp201Created_MapsReceiptAccurately()
    {
        // Arrange
        var mockResponse = new BlockchainAnchorTransportResponse(
            AuditRecordId: _testRecordId.ToString("D"),
            RecordHash: _testHash,
            AnchorStatus: "Anchored",
            TransactionReference: "0x1234567890abcdef",
            BlockNumber: 42,
            AnchoredAtUtc: new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc),
            ErrorMessage: null
        );

        var handler = new MockHttpMessageHandler(HttpStatusCode.Created, JsonSerializer.Serialize(mockResponse));
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8550/") };
        var service = new HttpBlockchainAuditAnchorService(client, NullLogger<HttpBlockchainAuditAnchorService>.Instance);

        var request = new AnchorAuditRequest(_testRecordId, _testHash, "RegulatoryCompliance", "AUDIT-V1");

        // Act
        var receipt = await service.AnchorRecordAsync(request);

        // Assert
        Assert.NotNull(receipt);
        Assert.Equal(_testRecordId, receipt.AuditRecordId);
        Assert.Equal(_testHash, receipt.RecordHash);
        Assert.Equal(AnchorStatus.Anchored, receipt.AnchorStatus);
        Assert.Equal("0x1234567890abcdef", receipt.TransactionReference);
        Assert.Equal(42, receipt.BlockNumber);
    }

    [Fact]
    public async Task AnchorRecordAsync_OnNetworkFailure_ReturnsUnavailableWithoutThrowing()
    {
        // Arrange - handler throws HttpRequestException
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused to Go microservice"));
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8550/") };
        var service = new HttpBlockchainAuditAnchorService(client, NullLogger<HttpBlockchainAuditAnchorService>.Instance);

        var request = new AnchorAuditRequest(_testRecordId, _testHash, "RegulatoryCompliance", "AUDIT-V1");

        // Act - Must NOT throw exception
        var receipt = await service.AnchorRecordAsync(request);

        // Assert
        Assert.NotNull(receipt);
        Assert.Equal(AnchorStatus.Unavailable, receipt.AnchorStatus);
        Assert.Null(receipt.TransactionReference);
    }

    [Fact]
    public async Task VerifyAuditIntegrityAsync_OnMatchResponse_MapsMatchStatus()
    {
        // Arrange
        var mockResponse = new BlockchainVerifyTransportResponse(
            AuditRecordId: _testRecordId.ToString("D"),
            CurrentHash: _testHash,
            AnchoredHash: _testHash,
            VerificationStatus: "Match",
            TransactionReference: "0x1234567890abcdef",
            AnchoredAtUtc: new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc),
            VerifiedAtUtc: new DateTime(2026, 10, 9, 11, 0, 0, DateTimeKind.Utc),
            Explanation: "The current off-chain record digest matches the anchored blockchain ledger record."
        );

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8550/") };
        var service = new HttpBlockchainAuditAnchorService(client, NullLogger<HttpBlockchainAuditAnchorService>.Instance);

        // Act
        var result = await service.VerifyAuditIntegrityAsync(_testRecordId, _testHash);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AuditVerificationStatus.Match, result.VerificationStatus);
        Assert.Equal(_testHash, result.CurrentHash);
        Assert.Equal(_testHash, result.AnchoredHash);
    }

    [Fact]
    public async Task VerifyAuditIntegrityAsync_OnMismatchResponse_MapsMismatchStatus()
    {
        // Arrange
        var mockResponse = new BlockchainVerifyTransportResponse(
            AuditRecordId: _testRecordId.ToString("D"),
            CurrentHash: _testHash,
            AnchoredHash: "different-hash",
            VerificationStatus: "Mismatch",
            TransactionReference: "0x1234567890abcdef",
            AnchoredAtUtc: new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc),
            VerifiedAtUtc: new DateTime(2026, 10, 9, 11, 0, 0, DateTimeKind.Utc),
            Explanation: "The current off-chain record does not match the anchored record version."
        );

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8550/") };
        var service = new HttpBlockchainAuditAnchorService(client, NullLogger<HttpBlockchainAuditAnchorService>.Instance);

        // Act
        var result = await service.VerifyAuditIntegrityAsync(_testRecordId, _testHash);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AuditVerificationStatus.Mismatch, result.VerificationStatus);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(_exception);
        }
    }
}
