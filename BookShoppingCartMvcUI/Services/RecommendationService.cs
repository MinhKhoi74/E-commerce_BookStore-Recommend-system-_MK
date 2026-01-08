using System.Text.Json;

namespace BookShoppingCartMvcUI.Services
{
    public class RecommendationService
    {
        private readonly HttpClient _httpClient;

        public RecommendationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Hàm gốc để gọi API dựa trên model
        private async Task<RecommendationResult> GetRecommendationsAsync(string model, string userId, int topN = 5, double alpha = 0.6)
        {
            string url = model.ToLower() switch
            {
                "usercf" => $"http://127.0.0.1:8000/recommend/usercf/{userId}?top_n={topN}&alpha={alpha}",
                "itemcf" => $"http://127.0.0.1:8000/recommend/itemcf/{userId}?top_n={topN}&alpha={alpha}",
                "mf" => $"http://127.0.0.1:8000/recommend/mf/{userId}?top_n={topN}",
                "best" => $"http://127.0.0.1:8000/recommend/best/{userId}?top_n={topN}", // model tốt nhất
                _ => throw new ArgumentException($"Unknown model: {model}")
            };

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            return JsonSerializer.Deserialize<RecommendationResult>(json, options);
        }

        // Các phương thức tiện lợi
        public Task<RecommendationResult> GetUserCFAsync(string userId, int topN = 5, double alpha = 0.6)
            => GetRecommendationsAsync("usercf", userId, topN, alpha);

        public Task<RecommendationResult> GetItemCFAsync(string userId, int topN = 5, double alpha = 0.6)
            => GetRecommendationsAsync("itemcf", userId, topN, alpha);

        public Task<RecommendationResult> GetMFAsync(string userId, int topN = 5)
            => GetRecommendationsAsync("mf", userId, topN);

        public Task<RecommendationResult> GetBestModelAsync(string userId, int topN = 5)
            => GetRecommendationsAsync("best", userId, topN);
    }
}
