using System.Numerics.Tensors;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OllamaSharp;
using OllamaSharp.Models;
using RagBm25HybridSearch.Configuration;
using RagBm25HybridSearch.Models;
using RagBm25HybridSearch.Prompts;

namespace RagBm25HybridSearch.Services
{
    public class OllamaApiService
    {
        private readonly ILogger<OllamaApiService> _logger;
        private readonly AppSettings _appSettings;
        private readonly AppConfig _appConfig;

        // Reusable API clients to prevent socket exhaustion
        private readonly OllamaApiClient _ollamaEmbedClient;
        private readonly OllamaApiClient _ollamaDecisionClient;

        // In-memory cache for label vectors to prevent repetitive disk I/O
        private List<LabelVectorModel>? _cachedLabelVectors;

        public OllamaApiService(
            ILogger<OllamaApiService> logger,
            AppSettings appSettings,
            AppConfig appConfig
        )
        {
            _logger = logger;
            _appSettings = appSettings;
            _appConfig = appConfig;

            _ollamaEmbedClient = new OllamaApiClient(_appConfig.UrlAPI!, _appConfig.EmbeddedModel!);
            _ollamaDecisionClient = new OllamaApiClient(
                _appConfig.UrlAPI!,
                _appConfig.DecisionModel!
            );
        }

        public async Task EmbeddedApi(List<string> labels)
        {
            try
            {
                var result = await _ollamaEmbedClient.EmbedAsync(
                    new EmbedRequest { Input = labels }
                );

                if (result?.Embeddings == null || result.Embeddings.Count == 0)
                {
                    _logger.LogWarning("OllamaApiService : EmbeddedApi No embeddings returned");
                    return;
                }

                var labelVectors = labels
                    .Zip(
                        result.Embeddings,
                        (label, vector) => new LabelVectorModel { Label = label, Vector = vector }
                    )
                    .ToList();

                // Save vectors to a JSON file for future use.
                // WriteAllTextAsync completely overwrites the existing file, preventing duplicate records.
                var json = JsonConvert.SerializeObject(labelVectors);
                var outputPath = _appSettings.FilePathEmbeddedJson!;
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await File.WriteAllTextAsync(outputPath, json);

                // Update in-memory cache immediately
                _cachedLabelVectors = labelVectors;
            }
            catch (Exception ex)
            {
                _logger.LogError($"OllamaApiService : EmbeddedApi Message = {ex.Message}");
            }
        }

        public async Task<List<string>> VectorSearchApi(string input)
        {
            List<string> candidateVector = new List<string>();
            try
            {
                // Lazy loading: Load vectors from disk only on the first search request
                if (_cachedLabelVectors == null)
                {
                    if (File.Exists(_appSettings.FilePathEmbeddedJson))
                    {
                        string json = await File.ReadAllTextAsync(
                            _appSettings.FilePathEmbeddedJson!
                        );
                        _cachedLabelVectors =
                            JsonConvert.DeserializeObject<List<LabelVectorModel>>(json)
                            ?? new List<LabelVectorModel>();
                    }
                    else
                    {
                        _logger.LogWarning("OllamaApiService : Embedded JSON file not found.");
                        return candidateVector;
                    }
                }

                EmbedResponse? result = await _ollamaEmbedClient.EmbedAsync(
                    new EmbedRequest { Input = new List<string> { input } }
                );

                if (result?.Embeddings == null || result.Embeddings.Count == 0)
                {
                    return candidateVector;
                }

                float[] inputVector = result.Embeddings[0];

                // Calculate Cosine Similarity to find the closest meaning
                var topCandidates = _cachedLabelVectors
                    .Select(lv => new
                    {
                        lv.Label,
                        Score = TensorPrimitives.CosineSimilarity(
                            new ReadOnlySpan<float>(inputVector),
                            new ReadOnlySpan<float>(lv.Vector)
                        ),
                    })
                    .OrderByDescending(x => x.Score)
                    .Take(10)
                    .ToList();

                // [Debug] Print the vector search candidates
                if (topCandidates.Count != 0)
                {
                    Console.WriteLine("\nVector Search:");
                    foreach (var item in topCandidates)
                    {
                        Console.WriteLine($"\t{item.Label} ({item.Score:F4})");
                    }
                }

                candidateVector = topCandidates.Select(x => x.Label).ToList()!;
            }
            catch (Exception ex)
            {
                _logger.LogError($"OllamaApiService : VectorSearchApi Message = {ex.Message}");
            }
            return candidateVector;
        }

        public async Task<ResultModel> ConfirmMatchApi(string input, List<string> candidates)
        {
            ResultModel resultModel = new ResultModel();
            try
            {
                string prompt = ConfirmMatchPrompt.ConfirmPrompt(input, candidates);

                var response = new StringBuilder();

                // Stream the response from the local LLM
                await foreach (var token in _ollamaDecisionClient.GenerateAsync(prompt))
                {
                    if (token != null && !string.IsNullOrEmpty(token.Response))
                    {
                        response.Append(token.Response);
                    }
                }

                resultModel.Keyword = response.ToString().Trim();

                // Validate if the LLM successfully found a match
                if (
                    !string.IsNullOrEmpty(resultModel.Keyword)
                    && !resultModel.Keyword.Contains("ไม่พบ")
                )
                {
                    resultModel.Success = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"OllamaApiService : ConfirmMatchApi Message = {ex.Message}");
            }
            return resultModel;
        }
    }
}
