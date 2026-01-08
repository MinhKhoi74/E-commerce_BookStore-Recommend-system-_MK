using BookShoppingCartMvcUI.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BookShoppingCartMvcUI.Data;        // DbContext của bạn
using BookShoppingCartMvcUI.Models;      // DashboardViewModel, Order, etc.

namespace BookShoppingCartMvcUI.Controllers;

[Authorize(Roles = nameof(Roles.Admin))]
public class AdminOperationsController : Controller
{
    private readonly IUserOrderRepository _userOrderRepository;
    private readonly ApplicationDbContext _context;

    public AdminOperationsController(
        IUserOrderRepository userOrderRepository,
        ApplicationDbContext context)
    {
        _userOrderRepository = userOrderRepository;
        _context = context;
    }

    // ================================================================
    // 📌 ALL ORDERS (Search + Filter + Pagination)
    // ================================================================
    public async Task<IActionResult> AllOrders(
        string? search,
        string? paymentStatus,
        int? orderStatus,
        int page = 1
    )
    {
        int pageSize = 10; // Số đơn trên 1 trang

        var orders = (await _userOrderRepository.UserOrders(true))
                        .OrderByDescending(o => o.CreateDate)
                        .AsQueryable();

        // ---------------- SEARCH ----------------
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            orders = orders.Where(o =>
                o.Name.ToLower().Contains(search) ||
                o.Email.ToLower().Contains(search) ||
                o.MobileNumber.ToLower().Contains(search)
            );
        }

        // ---------------- FILTER PAYMENT ----------------
        if (paymentStatus == "paid")
            orders = orders.Where(o => o.IsPaid == true);

        if (paymentStatus == "unpaid")
            orders = orders.Where(o => o.IsPaid == false);

        // ---------------- FILTER STATUS ----------------
        if (orderStatus.HasValue)
            orders = orders.Where(o => o.OrderStatusId == orderStatus.Value);

        // ---------------- PAGINATION ----------------
        int totalOrders = orders.Count();
        int totalPages = (int)Math.Ceiling(totalOrders / (double)pageSize);

        var pagedOrders = orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // ---------------- SEND TO VIEW ----------------
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;

        ViewBag.Search = search;
        ViewBag.PaymentStatus = paymentStatus;
        ViewBag.OrderStatus = orderStatus;

        ViewBag.OrderStatuses = await _userOrderRepository.GetOrderStatuses();

        return View(pagedOrders);
    }

    // ================================================================
    // 📌 Toggle thanh toán
    // ================================================================
    public async Task<IActionResult> TogglePaymentStatus(int orderId)
    {
        try
        {
            await _userOrderRepository.TogglePaymentStatus(orderId);
        }
        catch (Exception ex)
        {
            // log exception
        }

        return RedirectToAction(nameof(AllOrders));
    }

    // ================================================================
    // 📌 Update trạng thái đơn
    // ================================================================
    public async Task<IActionResult> UpdateOrderStatus(int orderId)
    {
        var order = await _userOrderRepository.GetOrderById(orderId);
        if (order == null)
        {
            throw new InvalidOperationException($"Order with id:{orderId} does not found.");
        }

        var orderStatusList = (await _userOrderRepository.GetOrderStatuses())
            .Select(orderStatus =>
                new SelectListItem
                {
                    Value = orderStatus.Id.ToString(),
                    Text = orderStatus.StatusName,
                    Selected = order.OrderStatusId == orderStatus.Id
                });

        var data = new UpdateOrderStatusModel
        {
            OrderId = orderId,
            OrderStatusId = order.OrderStatusId,
            OrderStatusList = orderStatusList
        };

        return View(data);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateOrderStatus(UpdateOrderStatusModel data)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                data.OrderStatusList = (await _userOrderRepository.GetOrderStatuses())
                    .Select(orderStatus =>
                        new SelectListItem
                        {
                            Value = orderStatus.Id.ToString(),
                            Text = orderStatus.StatusName,
                            Selected = orderStatus.Id == data.OrderStatusId
                        });

                return View(data);
            }

            await _userOrderRepository.ChangeOrderStatus(data);
            TempData["msg"] = "Updated successfully";
        }
        catch (Exception ex)
        {
            TempData["msg"] = "Something went wrong";
        }

        return RedirectToAction(nameof(UpdateOrderStatus), new { orderId = data.OrderId });
    }

    // ================================================================
    // 📌 Dashboard
    // ================================================================
    public async Task<IActionResult> Dashboard()
    {
        // Lấy 5 đơn hàng mới nhất chưa bị xóa
        var latestOrders = await _context.Orders
                                    .Where(o => !o.IsDeleted)
                                    .OrderByDescending(o => o.CreateDate)
                                    .Take(5)
                                    .ToListAsync();

        // Tạo ViewModel
        var vm = new DashboardViewModel
        {
            TotalRevenue = await _context.Orders
                                .Where(o => !o.IsDeleted && o.IsPaid)
                                .SumAsync(o => o.TotalAmount),

            TotalBooks = await _context.Books.CountAsync(),

            TotalStock = await _context.Stocks.SumAsync(s => s.Quantity),

            TotalUsers = await _context.Users.CountAsync(),

            TotalOrders = await _context.Orders.CountAsync(o => !o.IsDeleted),

            LatestOrders = latestOrders   // Gán danh sách 5 đơn hàng mới nhất
        };

        return View(vm);
    }


}
