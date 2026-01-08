namespace BookShoppingCartMvcUI.Models
{
    public class RecommendationItem
    {
        public int BookId { get; set; }
        public double Score { get; set; }
    }

    public class RecommendationResult
    {
        public string Model { get; set; }
        public string User { get; set; }
        public List<RecommendationItem> Recommendations { get; set; }
    }
}
