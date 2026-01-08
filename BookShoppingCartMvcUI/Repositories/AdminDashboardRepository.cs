/*using BookShoppingCartMvcUI.Data;
using BookShoppingCartMvcUI.Models;
using Microsoft.EntityFrameworkCore;

public class AdminDashboardRepository
{
    private readonly ApplicationDbContext _context;

    public AdminDashboardRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // Tổng số đơn hàng
    public async Task<int> GetTotalOrdersAsync()
    {
        return await _context.Orders.CountAsync();
    }

    // Tổng doanh thu (tính từ đơn đã thanh toán)
    public async Task<decimal> GetTotalRevenueAsync()
    {
        return await _context.Orders
            .Where(o => o.IsPaid)
            .SumAsync(o => o.OrderDetail != null ? o.OrderDetail.Sum(d => d.UnitPrice * d.Quantity) : 0);
    }

    // Tổng số sách đã bán
    public async Task<int> GetTotalBooksSoldAsync()
    {
        return await _context.Orders
            .Where(o => o.OrderDetail != null)
            .SumAsync(o => o.OrderDetail.Sum(d => d.Quantity));
    }

    // Tổng số lượng sách hiện có trong kho
    public async Task<int> GetTotalStockAsync()
    {
        return await _context.Books
            .SumAsync(b => b.Stock?.Quantity ?? 0); // null-safe
    }

    // 5 đơn hàng mới nhất
    public async Task<List<Order>> GetLatestOrdersAsync(int count = 5)
    {
        return await _context.Orders
            .Include(o => o.OrderDetail)
                .ThenInclude(d => d.Book)
                    .ThenInclude(b => b.Stock)
            .Include(o => o.OrderStatus)
            .OrderByDescending(o => o.CreateDate)
            .Take(count)
            .ToListAsync();
    }
}
*/