using drittich.ReciprocalRankFusion;
using Microsoft.Extensions.Logging;

namespace RagBm25HybridSearch.Services
{
    public class HybridSearch
    {
        private readonly ILogger<HybridSearch> _logger;

        public HybridSearch(ILogger<HybridSearch> logger)
        {
            _logger = logger;
        }

        public List<string> ReRankingKeyword(
            List<string> candidatesBM25,
            List<string> candidatesVector
        )
        {
            List<string> resultData = new List<string>();
            try
            {
                // Convert ranked lists into scoring dictionaries for RRF
                Dictionary<string, double> bm25Dic = (candidatesBM25 ?? new List<string>())
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Select(label => label.Trim().ToUpper())
                    .Distinct()
                    .Select((label, index) => new { label, index })
                    .ToDictionary(x => x.label, x => x.index * -1.0);

                Dictionary<string, double> vectorDic = (candidatesVector ?? new List<string>())
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Select(label => label.Trim().ToUpper())
                    .Distinct()
                    .Select((label, index) => new { label, index })
                    .ToDictionary(x => x.label, x => x.index * -1.0);

                // Combine results from both search engines
                var searchResultsDict = new Dictionary<string, Dictionary<string, double>>
                {
                    { "BM25", bm25Dic },
                    { "Vector", vectorDic },
                };

                // Apply Reciprocal Rank Fusion algorithm
                var fuser = new SearchResultFuser();
                var fusedResults = fuser.FuseSearchResults(searchResultsDict);

                // Retrieve the top 5 highest scored candidates
                var topNResults = fusedResults.Take(5).ToDictionary(x => x.Key, x => x.Value);

                // [Debug] Print the re-ranked candidates
                Console.WriteLine("\nRRF Search:");
                foreach (var result in topNResults)
                {
                    Console.WriteLine($"\t{result.Key} ({result.Value:F4})");
                    resultData.Add(result.Key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"HybridSearch : ReRankingKeyword Message = {ex.Message}");
            }
            return resultData;
        }
    }
}
