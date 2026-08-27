using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using StateLandGovernance.LeaseFeasibility.Application.Interfaces;
using StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

namespace OcrBatchTester
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Initializing OCR Batch Tester...");

            // Setup mock config to trigger the local file reader mock logic
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["Azure:DocumentIntelligence:Endpoint"]).Returns("https://dummy.cognitiveservices.azure.com/");
            var ocrService = new DocumentOcrService(configMock.Object);

            string mockDocsPath = Path.Combine("..", "mock-documents");
            if (!Directory.Exists(mockDocsPath))
            {
                Console.WriteLine($"Directory not found: {mockDocsPath}");
                return;
            }

            var files = Directory.GetFiles(mockDocsPath, "*.txt");
            Console.WriteLine($"Found {files.Length} mock documents to test.\n");

            int totalFields = 0;
            int totalCorrect = 0;

            foreach (var file in files)
            {
                string filename = Path.GetFileName(file);
                int docFields = 4; // Salary, Statement, CRIB all have 4 required fields
                int correctFields = docFields;
                
                Console.Write($"Testing [{filename}]... ");

                try
                {
                    if (filename.Contains("salary", StringComparison.OrdinalIgnoreCase))
                    {
                        await ocrService.ExtractSalarySlipDataAsync(file);
                    }
                    else if (filename.Contains("statement", StringComparison.OrdinalIgnoreCase))
                    {
                        await ocrService.ExtractBankStatementDataAsync(file);
                    }
                    else if (filename.Contains("crib", StringComparison.OrdinalIgnoreCase))
                    {
                        await ocrService.ExtractCribReportDataAsync(file);
                    }
                    else
                    {
                        Console.WriteLine("Skipped (Unknown document type).");
                        continue;
                    }
                    Console.WriteLine($"Success (4/4 fields)");
                }
                catch (ValidationException ex)
                {
                    // Each validation error usually means 1 missing field
                    int errors = ex.Errors.Count(e => e.Contains("Missing required field"));
                    
                    // If the indicator was not found, it's considered malformed, 0/4
                    if (ex.Errors.Any(e => e.Contains("Malformed document")))
                    {
                        correctFields = 0;
                    }
                    else
                    {
                        correctFields -= errors;
                    }

                    if (correctFields < 0) correctFields = 0;
                    
                    Console.WriteLine($"Failed ({correctFields}/{docFields} fields): {string.Join(" | ", ex.Errors)}");
                }
                catch (Exception ex)
                {
                    correctFields = 0;
                    Console.WriteLine($"Error (0/{docFields} fields): {ex.Message}");
                }

                totalFields += docFields;
                totalCorrect += correctFields;
            }

            Console.WriteLine("\n--- BATCH TEST SUMMARY ---");
            Console.WriteLine($"Total Documents Tested: {files.Length}");
            Console.WriteLine($"Overall Extraction Accuracy: {totalCorrect}/{totalFields} fields ({(double)totalCorrect/totalFields:P1})");
        }
    }
}
