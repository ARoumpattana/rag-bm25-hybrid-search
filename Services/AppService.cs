using KeyMatch.Configuration;
using KeyMatch.Interface;
using KeyMatch.Models;
using Microsoft.Extensions.Logging;

namespace KeyMatch.Services
{
    internal class AppService : IAppService
    {
        private readonly ILogger<AppService> _logger;
        private readonly OllamaApiService _ollamaApiService;
        private readonly FullTextSearchService _fullTextSearchService;
        private readonly HybridSearch _hybridSearch;
        private readonly AppSettings _appSettings;

        public AppService(
            ILogger<AppService> logger,
            OllamaApiService ollamaApiService,
            FullTextSearchService fullTextSearchService,
            HybridSearch hybridSearch,
            AppSettings appSettings
        )
        {
            _logger = logger;
            _ollamaApiService = ollamaApiService;
            _fullTextSearchService = fullTextSearchService;
            _hybridSearch = hybridSearch;
            _appSettings = appSettings;
        }

        public async Task RunAsync()
        {
            string stars = new string('*', 100);

            // Initialize databases if configured in appsettings.json
            if (_appSettings.RegisterData)
            {
                Console.WriteLine("Initializing Vector Database and Lucene Index... Please wait.");

                List<string> labels = new List<string>
                {
                    "TIRE",
                    "ANTI-VIBRATION PARTS",
                    "COOLING PAD",
                    "PAINTS",
                    "PARTICLE BOARDS",
                    "STAPLE FIBERS",
                    "FRESH LONGAN",
                    "Sugars",
                    "Vehicle parts",
                    "DRYWALL COLL",
                    "BISCOFF",
                    "Deodorant",
                    "FRESH COCONUT",
                    "WALL CHARGER",
                    "TYRES",
                    "CABINET HEATER",
                    "MONITOR SCREEN",
                    "ACID",
                    "OIL",
                    "PRINTING",
                    "STEEL",
                    "FOOD",
                    "ACRYLIC",
                    "AIR BAG",
                    "AIR COMPRESSOR",
                    "AIR CONDITIONER",
                    "AIR CONDITION",
                    "ALUMINIUM",
                    "JUICE",
                    "MOTORCYCLE",
                    "AUTO",
                    "AUTO PART",
                    "RADIATOR",
                    "BACON",
                    "RUBBER",
                };

                _fullTextSearchService.RegisterKeyword(labels);
                await _ollamaApiService.EmbeddedApi(labels);

                Console.WriteLine("Initialization Complete!\n");
            }

            try
            {
                do
                {
                    ResultModel resultModel = new ResultModel();
                    Console.Clear();
                    Console.WriteLine(stars);
                    Console.WriteLine(
                        " [Tip] Example: XYZ999::IMPORT:THLCH:FRESH LONGAN GRADE A PACKING:260825::CONTAINER-40FT"
                    );
                    Console.WriteLine(stars);
                    Console.Write("Enter Input: ");

                    string? rawInput = Console.ReadLine();

                    // Skip processing if user submits an empty string
                    if (string.IsNullOrWhiteSpace(rawInput))
                    {
                        continue;
                    }
                    string inputTest = rawInput.Trim();

                    // Step 1: Lexical Search (BM25)
                    List<string> candidatesBM25 =
                        _fullTextSearchService.SearchKeywordParser(inputTest) ?? new List<string>();

                    if (candidatesBM25.Count < 1)
                    {
                        // Fallback Case A: Semantic Search only (No exact keywords found)
                        Console.WriteLine("Candidates BM25 not found.");
                        List<string> candidatesVector =
                            await _ollamaApiService.VectorSearchApi(inputTest)
                            ?? new List<string>();

                        if (candidatesVector.Count > 0)
                        {
                            resultModel = await _ollamaApiService.ConfirmMatchApi(
                                inputTest,
                                candidatesVector
                            );
                        }
                    }
                    else
                    {
                        // Step 2: Semantic Search (Vector) for Hybrid approach
                        List<string> candidatesVector =
                            await _ollamaApiService.VectorSearchApi(inputTest)
                            ?? new List<string>();

                        if (candidatesVector.Count < 1)
                        {
                            // Fallback Case B: Lexical Search only (No vector embeddings matched)
                            Console.WriteLine("\nCandidates Vector not found.");
                            resultModel = await _ollamaApiService.ConfirmMatchApi(
                                inputTest,
                                candidatesBM25
                            );
                        }
                        else
                        {
                            // Step 3: Hybrid Search (RRF Re-ranking)
                            List<string> hybridSearchResult =
                                _hybridSearch.ReRankingKeyword(candidatesBM25, candidatesVector)
                                ?? new List<string>();

                            resultModel = await _ollamaApiService.ConfirmMatchApi(
                                inputTest,
                                hybridSearchResult
                            );
                        }
                    }

                    // Final Decision Output
                    if (resultModel.Success)
                    {
                        Console.WriteLine($"\nFinal Answer: \n\t{resultModel.Keyword}");
                    }
                    else
                    {
                        Console.WriteLine(
                            "\nLabel not found. Please check your input or label list."
                        );
                    }

                    Console.WriteLine("\nPress any key for next input...");
                    Console.ReadKey();
                } while (true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"AppService : RunAsync Message = {ex.Message}");
            }
        }
    }
}
