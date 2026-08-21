using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Intranet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Intranet.Services
{
    public class QuoteAnalysisResult
    {
        public string SupplierName { get; set; } = "";
        public string VatRegistration { get; set; } = "";
        public decimal TotalPrice { get; set; }
        public string Notes { get; set; } = "";
        public double ConfidenceScore { get; set; }
    }

    public class ComplianceResult
    {
        public bool IsValid { get; set; }
        public string ComparisonSummary { get; set; } = "";
        public string? ExtractedReference { get; set; }
    }

    public class GeminiAgentService
    {
        private readonly GoogleCredential _credential;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly string _endpoint;

        public GeminiAgentService(HttpClient httpClient, IConfiguration config, GoogleCredential credential)
        {
            _httpClient = httpClient;
            _config = config;
            _credential = credential;

            var projectId = _config["Gemini:ProjectId"];
            var location = _config["Gemini:Location"] ?? "us-central1";
            var modelName = _config["Gemini:ModelName"] ?? "gemini-2.5-flash"; 

            _endpoint = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{modelName}:generateContent";
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var scoped = _credential.CreateScoped("https://www.googleapis.com/auth/cloud-platform");
            var token = await scoped.UnderlyingCredential.GetAccessTokenForRequestAsync();
            return token ?? throw new InvalidOperationException("Failed to generate a valid Vertex AI OAuth2 Bearer Access Token.");
        }

        public async Task<QuoteAnalysisResult> AnalyzeQuoteAsync(string filePath, string originalFileName)
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            var base64Data = Convert.ToBase64String(bytes);

            var prompt = @"Analyze this document and extract the relevant quote values.
    CRITICAL RULES:
    1. If the document is blank, contains no numeric pricing, or is missing a document/quote number, set 'TotalPrice' to 0 and 'Notes' to 'INVALID_DOC'.
    2. If valid, extract data and return a JSON object adhering to the schema below.
    3. Do not wrap code in backticks or markdown formatting.

    {
        ""VatRegistration"": ""string description or empty"",
        ""TotalPrice"": 0.00,
        ""Notes"": ""brief breakdown summary text"",
        ""ConfidenceScore"": 0.95
    }

    CRITICAL: The ""Notes"" field value must be extremely concise and must not exceed 50 characters total.";

            var responseJson = await SendToGeminiMultimodalAsync(prompt, new[] { base64Data }, new[] { GetMimeType(filePath) }, forceJson: true);

            if (responseJson.StartsWith("```"))
            {
                responseJson = responseJson.Replace("```json", "").Replace("```", "").Trim();
            }

            var result = JsonSerializer.Deserialize<QuoteAnalysisResult>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                       ?? new QuoteAnalysisResult { Notes = "Extraction failed." };

            if (result.TotalPrice <= 0 || result.Notes == "INVALID_DOC")
            {
                result.Notes = "The uploaded document appears to be blank or missing required financial data (Price/Number).";
                result.ConfidenceScore = 0;
            }

            result.SupplierName = Path.GetFileNameWithoutExtension(originalFileName);

            return result;
        }

        public async Task<ComplianceResult> VerifyComplianceAsync(Quote selectedQuote, IFormFile invoice, IFormFile? pop)
        {
            var base64Datas = new List<string> { await ConvertToBase64(invoice) };
            var mimeTypes = new List<string> { invoice.ContentType };

            if (pop != null)
            {
                base64Datas.Add(await ConvertToBase64(pop));
                mimeTypes.Add(pop.ContentType);
            }

            var prompt = $@"ACT AS A STRICT AUDITOR. Compare the attached Invoice against these Approved Details:
                    - Approved Supplier Reference: {selectedQuote.SupplierName}
                    - Approved Amount: {selectedQuote.Price}
                    
                    CRITICAL RULES:
                    1. The 'Approved Supplier Reference' provided above is derived from an internal quote filename. 
                       Analyze the Invoice's issuer/vendor name and determine if it matches or closely resembles the core identity of the filename reference. 
                       (e.g., If the reference is 'Amazon_Server_Specs' and the invoice is from 'Amazon Web Services', this is a MATCH).
                       If the identity does not match, set IsValid to false.
                    2. If the Total/Grand Total on the invoice is not exactly {selectedQuote.Price}, set IsValid to false.
                    3. If the document is not an Invoice or Tax Invoice, set IsValid to false.
                    4. Provide a clear reason in 'ComparisonSummary' if IsValid is false.

                    Respond ONLY in JSON: {{ ""IsValid"": bool, ""ComparisonSummary"": ""string"" }}";

            try
            {
                var responseJson = await SendToGeminiMultimodalAsync(prompt, base64Datas.ToArray(), mimeTypes.ToArray(), forceJson: true);

                if (responseJson.StartsWith("```"))
                {
                    responseJson = responseJson.Replace("```json", "").Replace("```", "").Trim();
                }

                var result = JsonSerializer.Deserialize<ComplianceResult>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null)
                {
                    return new ComplianceResult { IsValid = false, ComparisonSummary = "The verification engine returned an empty result." };
                }

                return result;
            }
            catch (Exception ex)
            {
                return new ComplianceResult
                {
                    IsValid = false,
                    ComparisonSummary = $"Verification system error: {ex.Message}. Please try again."
                };
            }
        }

        public async Task<ComplianceResult> VerifyPopAsync(IFormFile popFile, string userEnteredReference, string approvedSupplierName, string requestType, bool isPurchaseOrder = false, bool isSpecial = false)
        {
            var base64Data = await ConvertToBase64(popFile);
            var mimeType = popFile.ContentType;

            string prompt;

            if (isPurchaseOrder)
            {
                prompt = $@"ACT AS A CORPORATE COMPLIANCE AUDITOR. 
                Compare this document against these Registered Parameters:
                - Approved Supplier Reference: '{approvedSupplierName}'
                - Expected PO Reference Number: '{userEnteredReference}'
                
                TASKS:
                1. Verify the attached document is an official Purchase Order (PO).
                2. Extract the Vendor/Supplier Name and PO Reference Number.
                
                CRITICAL RULES:
                - Vendor must match or closely resemble '{approvedSupplierName}'.
                - PO Reference found inside the file text must match '{userEnteredReference}'.
                
                Respond ONLY in JSON: {{ ""IsValid"": bool, ""ComparisonSummary"": ""string"", ""ExtractedReference"": ""string"" }}";
            }

            else if (isSpecial)
            {
                bool isWaybill = requestType.Equals("Waybill", StringComparison.OrdinalIgnoreCase);
                bool isOnline = requestType.Equals("ONLINE", StringComparison.OrdinalIgnoreCase);

                string vendorTask = (isWaybill || isOnline)
                    ? "1. Skip vendor verification (not applicable for Waybills)."
                    : $"1. Extract the Vendor Name and verify it resembles or closely matches '{approvedSupplierName}'.";

                prompt = $@"ACT AS A COMPLIANCE AUDITOR. 
    REQUIRED DATA: Target Value '{userEnteredReference}', Expected Supplier '{approvedSupplierName}'.
    TASKS: 
    {vendorTask}
    2. Scan the document for the specific value '{userEnteredReference}'. 
    3. Determine if '{userEnteredReference}' exists anywhere on the document.
    
    Respond ONLY in JSON (no markdown): {{ 
        ""IsValid"": bool, 
        ""ComparisonSummary"": ""{(isWaybill || isOnline ? "Logistics check: " : "Vendor found: [Name]. ")} Target value '{userEnteredReference}' found: [True/False]. Logic: [Explain findings]."", 
        ""ExtractedReference"": ""[The value you found]"" 
    }}";
            }
            else
            {
                prompt = $@"ACT AS A BANK AUDITOR. 
                Compare this document against these Approved Details:
                - Approved Supplier Reference: '{approvedSupplierName}'
                - Expected Reference: '{userEnteredReference}'
                
                TASKS:
                1. Verify the document is a Bank Proof of Payment.
                2. Extract the 'Recipient' name and 'Reference'.
                
                CRITICAL RULES:
                - Recipient must match or closely resemble: '{approvedSupplierName}'.
                - Reference must match: '{userEnteredReference}'.
                - Be smart with 'Pty Ltd' or 'Inc' suffixes—ignore them if the core name matches.
                
                Respond ONLY in JSON: {{ ""IsValid"": bool, ""ComparisonSummary"": ""string"", ""ExtractedReference"": ""string"" }}";
            }

            try
            {
                string rawResponse = await SendToGeminiMultimodalAsync(prompt, new[] { base64Data }, new[] { mimeType }, true);

                
                // 1. Find the first '{' and last '}' to isolate the JSON
                int start = rawResponse.IndexOf('{');
                int end = rawResponse.LastIndexOf('}');

                if (start == -1 || end == -1)
                    throw new Exception("AI did not return a valid JSON object.");

                string json = rawResponse.Substring(start, end - start + 1);

                // 2. Deserialize the cleaned JSON
                var result = JsonSerializer.Deserialize<ComplianceResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null) return new ComplianceResult { IsValid = false, ComparisonSummary = "Failed to parse JSON structure." };

                return result;
            }
            catch (Exception ex)
            {
                return new ComplianceResult { IsValid = false, ComparisonSummary = $"System Error: {ex.Message}" };
            }
        }

        private async Task<string> SendToGeminiMultimodalAsync(string prompt, string[] base64Datas, string[] mimeTypes, bool forceJson)
        {
            var token = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var parts = new List<object> { new { text = prompt } };
            for (int i = 0; i < base64Datas.Length; i++)
            {
                parts.Add(new { inline_data = new { mime_type = mimeTypes[i], data = base64Datas[i] } });
            }

            var requestBody = new
            {
                contents = new[] { new { role = "user", parts = parts } },
                generationConfig = forceJson ? new { responseMimeType = "application/json" } : null
            };

            var response = await _httpClient.PostAsJsonAsync(_endpoint, requestBody);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API Error: {error}");
            }

            var responseData = await response.Content.ReadAsStringAsync();
            return ExtractTextFromVertexResponse(responseData);
        }

        private string ExtractTextFromVertexResponse(string jsonResponse)
        {
            using var document = JsonDocument.Parse(jsonResponse);
            var root = document.RootElement;
            return root.GetProperty("candidates")[0]
                       .GetProperty("content")
                       .GetProperty("parts")[0]
                       .GetProperty("text").GetString() ?? "";
        }


        private async Task<string> ConvertToBase64(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return Convert.ToBase64String(ms.ToArray());
        }

        private string GetMimeType(string path) => Path.GetExtension(path).ToLower() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
    }
}