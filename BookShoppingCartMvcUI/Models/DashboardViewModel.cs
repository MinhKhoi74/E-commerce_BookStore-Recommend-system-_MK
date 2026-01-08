namespace BookShoppingCartMvcUI.Models
{
    // Models/DashboardViewModel.cs
    public class DashboardViewModel
    {
        public decimal TotalRevenue { get; set; }   // Tổng doanh thu
        public int TotalBooks { get; set; }         // Tổng sách
        public int TotalStock { get; set; }         // Tổng sách tồn kho
        public int TotalUsers { get; set; }         // Tổng người dùng
        public int TotalOrders { get; set; }        // Tổng đơn hàng
        public List<Order> LatestOrders { get; set; } = new List<Order>();
    }

}
