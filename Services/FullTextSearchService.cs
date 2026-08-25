using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Extensions.Logging;
using RagBm25HybridSearch.Configuration;
using RagBm25HybridSearch.Utils;
using Document = Lucene.Net.Documents.Document;
using LuceneDirectory = Lucene.Net.Store.Directory;
using OpenMode = Lucene.Net.Index.OpenMode;

namespace RagBm25HybridSearch.Services
{
    public class FullTextSearchService
    {
        private readonly ILogger<FullTextSearchService> _logger;
        private readonly AppSettings _appSettings;
        const LuceneVersion luceneVersion = LuceneVersion.LUCENE_48;

        public FullTextSearchService(ILogger<FullTextSearchService> logger, AppSettings appSettings)
        {
            _logger = logger;
            _appSettings = appSettings;
        }

        public void RegisterKeyword(List<string> originalData)
        {
            try
            {
                // Setup storage and text analyzer
                using LuceneDirectory indexDir = FSDirectory.Open(
                    _appSettings.FilePathFullTextSearch
                );
                using Analyzer standardAnalyzer = new StandardAnalyzer(luceneVersion);

                IndexWriterConfig indexConfig = new IndexWriterConfig(
                    luceneVersion,
                    standardAnalyzer
                );

                // OpenMode.CREATE forces a full overwrite of the existing index, preventing duplicate records.
                indexConfig.OpenMode = OpenMode.CREATE;

                using IndexWriter writer = new IndexWriter(indexDir, indexConfig);

                // Save all keywords into the search index
                foreach (var data in originalData)
                {
                    Document doc = new Document();
                    doc.Add(new TextField("registeredKeyword", data, Field.Store.YES));
                    writer.AddDocument(doc);
                }

                writer.Commit(); // Save to disk
            }
            catch (Exception ex)
            {
                _logger.LogError($"FullTextSearchService : LuceneTest Message = {ex.Message}");
            }
        }

        public void SearchKeyword(string rawInput)
        {
            try
            {
                string input = CleanUpTextUtils.CleanUpStandardTextSearch(rawInput);
                if (string.IsNullOrWhiteSpace(input))
                    return; // Skip empty inputs

                using FSDirectory indexDir = FSDirectory.Open(_appSettings.FilePathFullTextSearch);
                using IndexReader reader = DirectoryReader.Open(indexDir);

                IndexSearcher searcher = new IndexSearcher(reader)
                {
                    Similarity = new BM25Similarity(), // Use BM25 scoring
                };

                Query query = new TermQuery(new Term("registeredKeyword", input));
                TopDocs topDocs = searcher.Search(query, n: 10); // Get top 10 matches

                int numMatchingDocs = topDocs.TotalHits;

                if (numMatchingDocs > 0)
                {
                    Document resultDoc = searcher.Doc(topDocs.ScoreDocs[0].Doc);
                    string title = resultDoc.Get("registeredKeyword");

                    Console.WriteLine($"Matching results: {numMatchingDocs}");
                    Console.WriteLine($"Title of first result: {title}");
                }
                else
                {
                    Console.WriteLine("Not Found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"FullTextSearchService : LuceneTest Message = {ex.Message}");
            }
        }

        public List<string> SearchKeywordParser(string rawInput)
        {
            List<string> resultList = new List<string>();
            string matchedKeyword = string.Empty;

            try
            {
                string input = CleanUpTextUtils.CleanUpStandardTextSearch(rawInput);
                if (string.IsNullOrWhiteSpace(input))
                    return resultList; // Skip empty inputs

                using LuceneDirectory indexDir = FSDirectory.Open(
                    _appSettings.FilePathFullTextSearch
                );
                using DirectoryReader reader = DirectoryReader.Open(indexDir);

                IndexSearcher searcher = new IndexSearcher(reader)
                {
                    Similarity = new BM25Similarity(), // Use BM25 scoring
                };

                using Analyzer standardAnalyzer = new StandardAnalyzer(luceneVersion);
                QueryParser parser = new QueryParser(
                    luceneVersion,
                    "registeredKeyword",
                    standardAnalyzer
                );

                Query query = parser.Parse(input);
                TopDocs topDocs = searcher.Search(query, 10); // Get top 10 matches

                // Extract keywords from the search results
                for (int i = 0; i < topDocs.ScoreDocs.Length; i++)
                {
                    Document resultDoc = searcher.Doc(topDocs.ScoreDocs[i].Doc);
                    matchedKeyword = resultDoc.Get("registeredKeyword");

                    if (!string.IsNullOrEmpty(matchedKeyword))
                    {
                        resultList.Add(matchedKeyword);
                    }
                }

                // Print debug results
                if (resultList.Count != 0)
                {
                    Console.WriteLine("\nBM25 Search (TOP TO FINAL):");
                    foreach (var item in resultList)
                    {
                        Console.WriteLine($"\t{item}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"FullTextSearchService : LuceneTest Message = {ex.Message}");
            }
            return resultList;
        }
    }
}
