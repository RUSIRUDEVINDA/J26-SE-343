namespace StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Infrastructure;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Exceptions;
using Xunit;

public sealed class FastApiDocumentIntelligenceServiceTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public byte[]? CapturedRequestBody { get; private set; }
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; set; } =
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                CapturedRequestBody = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            return await Handler(request, cancellationToken);
        }
    }

    private sealed class FakeDocumentContentReader : IDocumentContentReader
    {
        public Stream? StreamToReturn { get; set; }

        public Task<Stream?> ReadContentAsync(
            GovernedDocumentId documentId,
            DocumentVersionId versionId,
            string contentReference,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StreamToReturn);
        }
    }

    private readonly FakeDocumentContentReader _contentReader = new();
    private readonly InMemoryAnalysisArtifactWriter _artifactWriter = new();
    private readonly DocumentIntelligenceOptions _options = new()
    {
        BaseUrl = "http://127.0.0.1:8000",
        RequestTimeoutSeconds = 30,
        DefaultLanguageMode = "sin+eng",
        Preprocess = true
    };

    private FastApiDocumentIntelligenceService CreateService(
        FakeHttpMessageHandler handler,
        IAnalysisArtifactWriter? artifactWriter = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_options.BaseUrl)
        };
        var optionsWrapper = Options.Create(_options);
        return new FastApiDocumentIntelligenceService(
            httpClient,
            optionsWrapper,
            NullLogger<FastApiDocumentIntelligenceService>.Instance,
            _contentReader,
            artifactWriter ?? _artifactWriter);
    }

    private static DocumentIntelligenceRequest CreateSampleRequest(
        string? languageHint = null,
        string mediaType = "application/pdf",
        string fileName = "deed.pdf")
    {
        return new DocumentIntelligenceRequest(
            GovernedDocumentId: Guid.NewGuid(),
            DocumentVersionId: Guid.NewGuid(),
            ContentReference: "store://docs/deed-v1.pdf",
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            OriginalFileName: fileName,
            MediaType: mediaType,
            LogicalCategory: "Deed",
            RequestedCapabilities: new[] { "Ocr" },
            LanguageHint: languageHint);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_SendsCorrectMultipartRequestAndCorrelationHeaders()
    {
        var request = CreateSampleRequest();
        var sampleBytes = Encoding.UTF8.GetBytes("%PDF-1.4 test document stream");
        _contentReader.StreamToReturn = new MemoryStream(sampleBytes);

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        request_id = request.DocumentVersionId.ToString("D"),
                        language_mode = "sin+eng",
                        preprocessed = true,
                        page_count = 1,
                        pages = new[]
                        {
                            new
                            {
                                page_number = 1,
                                raw_text = "STATE LAND LEASE\n",
                                clean_text = "STATE LAND LEASE",
                                text = "STATE LAND LEASE",
                                character_count = 16,
                                confidence = 94.5
                            }
                        },
                        total_character_count = 16,
                        duration_ms = 120.5
                    }),
                    Encoding.UTF8,
                    "application/json")
            })
        };

        var service = CreateService(handler);
        var result = await service.AnalyzeDocumentAsync(request);

        Assert.NotNull(result);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.StartsWith("http://127.0.0.1:8000/v1/ocr", handler.LastRequest.RequestUri!.ToString());
        Assert.Contains("language_mode=sin%2Beng", handler.LastRequest.RequestUri.Query);
        Assert.Contains("preprocess=true", handler.LastRequest.RequestUri.Query);

        // Verify headers
        Assert.True(handler.LastRequest.Headers.Contains("X-Request-ID"));
        Assert.Equal(request.DocumentVersionId.ToString("D"),
            handler.LastRequest.Headers.GetValues("X-Request-ID").First());
        Assert.True(handler.LastRequest.Headers.Contains("X-Correlation-ID"));
        Assert.Equal(request.DocumentVersionId.ToString("D"),
            handler.LastRequest.Headers.GetValues("X-Correlation-ID").First());

        // Verify multipart content from captured request stream
        Assert.NotNull(handler.CapturedRequestBody);
        Assert.True(handler.CapturedRequestBody.Length > 0);
        var bodyText = Encoding.UTF8.GetString(handler.CapturedRequestBody);
        Assert.Contains("name=file", bodyText);
        Assert.Contains($"{request.DocumentVersionId:D}.pdf", bodyText);
        Assert.Contains("%PDF-1.4 test document stream", bodyText);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_DeserilizesExactPythonContract_PreservesAllRequiredFields()
    {
        // Representative response matching exact Pydantic OCRResponse schema from FastAPI
        var rawJson = """
        {
            "request_id": "test-req-12345",
            "language_mode": "sin+eng",
            "preprocessed": true,
            "page_count": 1,
            "pages": [
                {
                    "page_number": 1,
                    "raw_text": "ශ්‍රී ලංකා ඉඩම්\r\nSTATE LAND",
                    "clean_text": "ශ්‍රී ලංකා ඉඩම්\nSTATE LAND",
                    "text": "ශ්‍රී ලංකා ඉඩම්\nSTATE LAND",
                    "character_count": 25,
                    "confidence": 91.5
                }
            ],
            "total_character_count": 25,
            "duration_ms": 315.42
        }
        """;

        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("fake-bytes"));

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(rawJson, Encoding.UTF8, "application/json")
            })
        };

        var service = CreateService(handler);
        var result = await service.AnalyzeDocumentAsync(request);

        Assert.NotNull(result);
        Assert.Equal("Succeeded", result.Outcome);
        Assert.Equal("FastApi-TesseractOCR", result.Provider);
        Assert.Equal("Tesseract-5", result.ModelName);
        Assert.Equal("5.4.0", result.ModelVersion);
        Assert.Equal(2, result.Artifacts.Count);

        // Verify page artifact has preserved clean and raw text via JSON provenance DTO
        var pageArtifact = Assert.Single(result.Artifacts, a => a.ArtifactKind == "OcrPageTranscript");
        Assert.Equal($"analysis-artifacts/{request.DocumentVersionId:D}/pages/1.json", pageArtifact.StorageReference);
        Assert.Equal("application/json", pageArtifact.ContentType);
        Assert.Equal("SHA-256", pageArtifact.ChecksumAlgorithm);

        // Verify warnings recorded optical confidence without promoting to legal fact confidence
        Assert.NotNull(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Contains("91.5%"));
    }

    [Theory]
    [InlineData("eng", "eng")]
    [InlineData("English", "eng")]
    [InlineData("sin", "sin")]
    [InlineData("Sinhala", "sin")]
    [InlineData("sin+eng", "sin+eng")]
    [InlineData("mixed", "sin+eng")]
    [InlineData("both", "sin+eng")]
    [InlineData(null, "sin+eng")] // fallback to default
    [InlineData("unrecognized", "sin+eng")] // fallback to default
    public async Task AnalyzeDocumentAsync_LanguageHintMapping_SendsExpectedLanguageQueryParam(
        string? inputHint,
        string expectedQueryMode)
    {
        var request = CreateSampleRequest(languageHint: inputHint);
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        request_id = "test",
                        language_mode = expectedQueryMode,
                        preprocessed = true,
                        page_count = 1,
                        pages = new[]
                        {
                            new { page_number = 1, raw_text = "t", clean_text = "t", text = "t", character_count = 1, confidence = (double?)80.0 }
                        },
                        total_character_count = 1,
                        duration_ms = 50.0
                    }),
                    Encoding.UTF8,
                    "application/json")
            })
        };

        var service = CreateService(handler);
        await service.AnalyzeDocumentAsync(request);

        Assert.NotNull(handler.LastRequest);
        var expectedEscaped = Uri.EscapeDataString(expectedQueryMode);
        Assert.Contains($"language_mode={expectedEscaped}", handler.LastRequest.RequestUri!.Query);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_MultiplePages_PreservesPageOrderAndProvenance()
    {
        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("pdf"));

        // Pages in reverse order from service to verify client ordering
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        request_id = "multi-page",
                        language_mode = "sin+eng",
                        preprocessed = true,
                        page_count = 2,
                        pages = new[]
                        {
                            new { page_number = 2, raw_text = "Page 2 text\n", clean_text = "Page 2 text", text = "Page 2 text", character_count = 11, confidence = (double?)92.0 },
                            new { page_number = 1, raw_text = "Page 1 text\n", clean_text = "Page 1 text", text = "Page 1 text", character_count = 11, confidence = (double?)96.0 }
                        },
                        total_character_count = 22,
                        duration_ms = 200.0
                    }),
                    Encoding.UTF8,
                    "application/json")
            })
        };

        var service = CreateService(handler);
        var result = await service.AnalyzeDocumentAsync(request);

        Assert.NotNull(result);
        Assert.Equal("Succeeded", result.Outcome);

        // 1 composite document transcript + 2 page transcripts = 3 artifacts
        Assert.Equal(3, result.Artifacts.Count);

        var docArtifact = result.Artifacts[0];
        Assert.Equal("OcrDocumentTranscript", docArtifact.ArtifactKind);
        Assert.Equal($"analysis-artifacts/{request.DocumentVersionId:D}/document-transcript.txt", docArtifact.StorageReference);

        var page1Artifact = result.Artifacts[1];
        Assert.Equal("OcrPageTranscript", page1Artifact.ArtifactKind);
        Assert.Equal($"analysis-artifacts/{request.DocumentVersionId:D}/pages/1.json", page1Artifact.StorageReference);
        Assert.False(string.IsNullOrWhiteSpace(page1Artifact.ChecksumValue));

        var page2Artifact = result.Artifacts[2];
        Assert.Equal("OcrPageTranscript", page2Artifact.ArtifactKind);
        Assert.Equal($"analysis-artifacts/{request.DocumentVersionId:D}/pages/2.json", page2Artifact.StorageReference);
        Assert.False(string.IsNullOrWhiteSpace(page2Artifact.ChecksumValue));
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_CrucialSemanticInvariant_ExtractedFactsIsEmpty()
    {
        // Core governance protection: OCR produces text recognition only.
        // It does NOT perform semantic classification or fact extraction.
        // Candidates must be strictly empty.
        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("bytes"));

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        request_id = "test",
                        language_mode = "sin+eng",
                        preprocessed = true,
                        page_count = 1,
                        pages = new[]
                        {
                            new { page_number = 1, raw_text = "Applicant: John Doe, Extent: 5 Acres", clean_text = "Applicant: John Doe, Extent: 5 Acres", text = "Applicant: John Doe, Extent: 5 Acres", character_count = 36, confidence = (double?)98.0 }
                        },
                        total_character_count = 36,
                        duration_ms = 85.0
                    }),
                    Encoding.UTF8,
                    "application/json")
            })
        };

        var service = CreateService(handler);
        var result = await service.AnalyzeDocumentAsync(request);

        Assert.NotNull(result);
        Assert.NotNull(result.Candidates);
        Assert.Empty(result.Candidates); // ZERO facts extracted!
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_SinhalaAndMixedUnicode_PreservesCharactersWithoutCorruption()
    {
        var sinhalaText = "ශ්‍රී ලංකා ප්‍රජාතාන්ත්‍රික සමාජවාදී ජනරජය";
        var mixedText = "Application No: APP-2026 / ලිපි අංකය: 2026/01";

        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("unicode-stream"));

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        request_id = "unicode-test",
                        language_mode = "sin+eng",
                        preprocessed = true,
                        page_count = 2,
                        pages = new[]
                        {
                            new { page_number = 1, raw_text = sinhalaText, clean_text = sinhalaText, text = sinhalaText, character_count = sinhalaText.Length, confidence = (double?)92.0 },
                            new { page_number = 2, raw_text = mixedText, clean_text = mixedText, text = mixedText, character_count = mixedText.Length, confidence = (double?)95.0 }
                        },
                        total_character_count = sinhalaText.Length + mixedText.Length,
                        duration_ms = 250.0
                    }),
                    Encoding.UTF8,
                    "application/json")
            })
        };

        var service = CreateService(handler);
        var result = await service.AnalyzeDocumentAsync(request);

        Assert.NotNull(result);
        Assert.Equal(3, result.Artifacts.Count);

        // Verify SHA-256 calculation covers exact UTF-8 bytes without charmap exception
        var page1 = result.Artifacts[1];
        var expectedPayload = new FastApiPageTranscriptArtifactDto(
            PageNumber: 1,
            LanguageMode: "sin+eng",
            RawText: sinhalaText,
            CleanText: sinhalaText);
        var expectedBytes = JsonSerializer.SerializeToUtf8Bytes(expectedPayload);
        var expectedHash = Convert.ToHexString(SHA256.HashData(expectedBytes)).ToLowerInvariant();
        Assert.Equal(expectedHash, page1.ChecksumValue);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, typeof(DocumentIntelligenceValidationException))]
    [InlineData((HttpStatusCode)413, typeof(DocumentIntelligencePayloadTooLargeException))]
    [InlineData(HttpStatusCode.UnsupportedMediaType, typeof(DocumentIntelligenceUnsupportedMediaException))]
    [InlineData(HttpStatusCode.UnprocessableEntity, typeof(DocumentIntelligenceLanguageUnavailableException))]
    [InlineData(HttpStatusCode.ServiceUnavailable, typeof(DocumentIntelligenceServiceUnavailableException))]
    [InlineData(HttpStatusCode.InternalServerError, typeof(DocumentIntelligenceServerException))]
    public async Task AnalyzeDocumentAsync_HttpErrorCodes_TranslatesToSanitizedExceptions(
        HttpStatusCode statusCode,
        Type expectedExceptionType)
    {
        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("sample"));

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        detail = "Specific error detail for testing: C:\\Users\\developer\\secret\\test.pdf",
                        error_code = "TEST_ERROR",
                        request_id = "err-123"
                    }),
                    Encoding.UTF8,
                    "application/json")
            })
        };

        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync(expectedExceptionType, () => service.AnalyzeDocumentAsync(request));
        Assert.Contains("Specific error detail for testing", ex.Message);
        Assert.DoesNotContain("C:\\", ex.Message); // Must not leak local machine file paths
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_PythonTracebackInError_SanitizesMessageCompletely()
    {
        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("sample"));

        var rawTraceback = """
        Traceback (most recent call last):
          File "C:\repo\service\app\main.py", line 42, in process
            raise RuntimeError("Database connection password=secret123")
        RuntimeError: internal failure
        """;

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(rawTraceback, Encoding.UTF8, "text/plain")
            })
        };

        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<DocumentIntelligenceServerException>(
            () => service.AnalyzeDocumentAsync(request));

        Assert.DoesNotContain("Traceback", ex.Message);
        Assert.DoesNotContain("secret123", ex.Message);
        Assert.DoesNotContain("C:\\repo", ex.Message);
        Assert.Contains("internal error occurred", ex.Message);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_Timeout_ThrowsDocumentIntelligenceTimeoutException()
    {
        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => throw new TaskCanceledException("HttpClient timeout")
        };

        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<DocumentIntelligenceTimeoutException>(
            () => service.AnalyzeDocumentAsync(request));

        Assert.Contains("timed out", ex.Message);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_UserCancellation_PropagatesOperationCanceledException()
    {
        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, token) => throw new OperationCanceledException(token)
        };

        var service = CreateService(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AnalyzeDocumentAsync(request, cts.Token));
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_MissingContentStream_ThrowsFileNotFoundException()
    {
        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = null;

        var handler = new FakeHttpMessageHandler();
        var service = CreateService(handler);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.AnalyzeDocumentAsync(request));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("ftp://localhost:8000")]
    public void DocumentIntelligenceOptions_InvalidBaseUrl_ThrowsArgumentException(string invalidUrl)
    {
        var options = new DocumentIntelligenceOptions { BaseUrl = invalidUrl };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DocumentIntelligenceOptions_InvalidTimeout_ThrowsArgumentOutOfRangeException(int timeout)
    {
        var options = new DocumentIntelligenceOptions
        {
            BaseUrl = "http://127.0.0.1:8000",
            RequestTimeoutSeconds = timeout
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData("french")]
    [InlineData("invalid")]
    public void DocumentIntelligenceOptions_InvalidLanguageMode_ThrowsArgumentException(string mode)
    {
        var options = new DocumentIntelligenceOptions
        {
            BaseUrl = "http://127.0.0.1:8000",
            DefaultLanguageMode = mode
        };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void AddWorkflowGovernanceDocumentIntelligence_RegistersServiceAndOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IDocumentContentReader>(_ => new FakeDocumentContentReader());
        services.AddScoped<IAnalysisArtifactWriter>(_ => new InMemoryAnalysisArtifactWriter());
        services.AddWorkflowGovernanceDocumentIntelligence(options =>
        {
            options.BaseUrl = "http://127.0.0.1:8001";
            options.DefaultLanguageMode = "sin";
            options.RequestTimeoutSeconds = 45;
        });

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<DocumentIntelligenceOptions>>().Value;
        Assert.Equal("http://127.0.0.1:8001", options.BaseUrl);
        Assert.Equal("sin", options.DefaultLanguageMode);
        Assert.Equal(45, options.RequestTimeoutSeconds);

        var client = sp.GetRequiredService<IDocumentIntelligenceService>();
        Assert.NotNull(client);
        Assert.IsType<FastApiDocumentIntelligenceService>(client);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_SuccessfulOcr_PersistsDocumentTranscriptAndPageTranscriptsToWriter()
    {
        // 1. Arrange request and simulated OCR response
        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("fake-bytes"));

        var responsePayload = new
        {
            request_id = request.DocumentVersionId.ToString("D"),
            language_mode = "sin+eng",
            preprocessed = true,
            page_count = 1,
            pages = new[]
            {
                new
                {
                    page_number = 1,
                    raw_text = "ශ්‍රී ලංකා රජයේ ඉඩම් බදු ගිවිසුම\r\nSTATE LAND LEASE",
                    clean_text = "ශ්‍රී ලංකා රජයේ ඉඩම් බදු ගිවිසුම\nSTATE LAND LEASE",
                    text = "ශ්‍රී ලංකා රජයේ ඉඩම් බදු ගිවිසුම\nSTATE LAND LEASE",
                    character_count = 52,
                    confidence = 96.0
                }
            },
            total_character_count = 52,
            duration_ms = 220.0
        };

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(responsePayload), Encoding.UTF8, "application/json")
            })
        };

        var service = CreateService(handler);

        // 2. Act
        var result = await service.AnalyzeDocumentAsync(request);

        // 3. Assert - Application Result
        Assert.NotNull(result);
        Assert.Equal("Succeeded", result.Outcome);
        Assert.Equal(2, result.Artifacts.Count);
        Assert.Empty(result.Candidates); // ZERO semantic facts

        // 4. Assert - Document transcript retrievable from writer and matches checksum
        var docArtifact = Assert.Single(result.Artifacts, a => a.ArtifactKind == "OcrDocumentTranscript");
        Assert.True(_artifactWriter.TryGetArtifact(docArtifact.StorageReference, out var storedDoc));
        Assert.NotNull(storedDoc);
        Assert.Equal("text/plain; charset=utf-8", storedDoc.ContentType);
        var storedDocText = Encoding.UTF8.GetString(storedDoc.Bytes);
        Assert.Equal("ශ්‍රී ලංකා රජයේ ඉඩම් බදු ගිවිසුම\nSTATE LAND LEASE", storedDocText);

        var computedDocHash = Convert.ToHexString(SHA256.HashData(storedDoc.Bytes)).ToLowerInvariant();
        Assert.Equal(docArtifact.ChecksumValue, computedDocHash);
        Assert.Equal(storedDoc.ChecksumValue, computedDocHash);

        // 5. Assert - Page transcript retrievable from writer, contains exact provenance JSON
        var pageArtifact = Assert.Single(result.Artifacts, a => a.ArtifactKind == "OcrPageTranscript");
        Assert.True(_artifactWriter.TryGetArtifact(pageArtifact.StorageReference, out var storedPage));
        Assert.NotNull(storedPage);
        Assert.Equal("application/json", storedPage.ContentType);

        var pageProvenance = JsonSerializer.Deserialize<FastApiPageTranscriptArtifactDto>(storedPage.Bytes);
        Assert.NotNull(pageProvenance);
        Assert.Equal(1, pageProvenance.PageNumber);
        Assert.Equal("sin+eng", pageProvenance.LanguageMode);
        Assert.Equal("ශ්‍රී ලංකා රජයේ ඉඩම් බදු ගිවිසුම\r\nSTATE LAND LEASE", pageProvenance.RawText);
        Assert.Equal("ශ්‍රී ලංකා රජයේ ඉඩම් බදු ගිවිසුම\nSTATE LAND LEASE", pageProvenance.CleanText);

        var computedPageHash = Convert.ToHexString(SHA256.HashData(storedPage.Bytes)).ToLowerInvariant();
        Assert.Equal(pageArtifact.ChecksumValue, computedPageHash);
        Assert.Equal(storedPage.ChecksumValue, computedPageHash);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_MultiplePages_PersistsSeparatelyAndOrdered()
    {
        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("multi-page-bytes"));

        var responsePayload = new
        {
            request_id = request.DocumentVersionId.ToString("D"),
            language_mode = "eng",
            preprocessed = true,
            page_count = 2,
            pages = new[]
            {
                new
                {
                    page_number = 2,
                    raw_text = "Page 2 Terms",
                    clean_text = "Page 2 Terms",
                    text = "Page 2 Terms",
                    character_count = 12,
                    confidence = 90.0
                },
                new
                {
                    page_number = 1,
                    raw_text = "Page 1 Header",
                    clean_text = "Page 1 Header",
                    text = "Page 1 Header",
                    character_count = 13,
                    confidence = 95.0
                }
            },
            total_character_count = 25,
            duration_ms = 350.0
        };

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(responsePayload), Encoding.UTF8, "application/json")
            })
        };

        var service = CreateService(handler);
        var result = await service.AnalyzeDocumentAsync(request);

        Assert.NotNull(result);
        Assert.Equal(3, result.Artifacts.Count); // 1 document + 2 pages

        // Verify ordering: page 1 before page 2
        var page1 = result.Artifacts.First(a => a.ArtifactKind == "OcrPageTranscript" && a.StorageReference.EndsWith("/1.json"));
        var page2 = result.Artifacts.First(a => a.ArtifactKind == "OcrPageTranscript" && a.StorageReference.EndsWith("/2.json"));

        Assert.True(_artifactWriter.TryGetArtifact(page1.StorageReference, out var stored1));
        Assert.True(_artifactWriter.TryGetArtifact(page2.StorageReference, out var stored2));
        Assert.NotNull(stored1);
        Assert.NotNull(stored2);

        var p1Dto = JsonSerializer.Deserialize<FastApiPageTranscriptArtifactDto>(stored1.Bytes);
        var p2Dto = JsonSerializer.Deserialize<FastApiPageTranscriptArtifactDto>(stored2.Bytes);
        Assert.Equal(1, p1Dto!.PageNumber);
        Assert.Equal("Page 1 Header", p1Dto.CleanText);
        Assert.Equal(2, p2Dto!.PageNumber);
        Assert.Equal("Page 2 Terms", p2Dto.CleanText);

        // Verify document transcript combines in page order
        var docArtifact = result.Artifacts.First(a => a.ArtifactKind == "OcrDocumentTranscript");
        var docStored = _artifactWriter.GetArtifact(docArtifact.StorageReference);
        var combinedText = Encoding.UTF8.GetString(docStored.Bytes);
        Assert.Equal("Page 1 Header\n\nPage 2 Terms", combinedText);
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_ArtifactWriterFails_ThrowsSanitizedExceptionAndReturnsNoResult()
    {
        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("sample"));

        var responsePayload = new
        {
            request_id = request.DocumentVersionId.ToString("D"),
            language_mode = "eng",
            preprocessed = true,
            page_count = 1,
            pages = new[]
            {
                new
                {
                    page_number = 1,
                    raw_text = "Text",
                    clean_text = "Text",
                    text = "Text",
                    character_count = 4,
                    confidence = 90.0
                }
            },
            total_character_count = 4,
            duration_ms = 50.0
        };

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(responsePayload), Encoding.UTF8, "application/json")
            })
        };

        _artifactWriter.OnBeforeWrite = (key, _) =>
            throw new IOException($"Simulated disk failure accessing D:\\data\\storage\\{key}");

        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<DocumentIntelligenceServerException>(
            () => service.AnalyzeDocumentAsync(request));

        Assert.Contains("Failed to persist document analysis artifact to storage", ex.Message);
        Assert.DoesNotContain("D:\\data\\storage", ex.Message); // Must be sanitized
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_CancellationDuringWrite_PropagatesOperationCanceledException()
    {
        var request = CreateSampleRequest();
        _contentReader.StreamToReturn = new MemoryStream(Encoding.UTF8.GetBytes("sample"));

        var responsePayload = new
        {
            request_id = request.DocumentVersionId.ToString("D"),
            language_mode = "eng",
            preprocessed = true,
            page_count = 1,
            pages = new[]
            {
                new
                {
                    page_number = 1,
                    raw_text = "Text",
                    clean_text = "Text",
                    text = "Text",
                    character_count = 4,
                    confidence = 90.0
                }
            },
            total_character_count = 4,
            duration_ms = 50.0
        };

        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(responsePayload), Encoding.UTF8, "application/json")
            })
        };

        using var cts = new CancellationTokenSource();

        _artifactWriter.OnBeforeWrite = (_, _) =>
        {
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };

        var service = CreateService(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AnalyzeDocumentAsync(request, cts.Token));
    }
}
