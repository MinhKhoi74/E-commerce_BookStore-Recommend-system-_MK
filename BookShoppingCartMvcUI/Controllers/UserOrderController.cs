using BookShoppingCartMvcUI.Data;
using BookShoppingCartMvcUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShoppingCartMvcUI.Controllers
{
    [Authorize]
    public class UserOrderController : Controller
    {
        private readonly IUserOrderRepository _userOrderRepo;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public UserOrderController(
            IUserOrderRepository userOrderRepo,
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager)
        {
            _userOrderRepo = userOrderRepo;
            _context = context;
            _userManager = userManager;
        }

        // ✅ Hiển thị danh sách đơn hàng
        public async Task<IActionResult> UserOrders()
        {
            var orders = await _userOrderRepo.UserOrders();
            return View(orders);
        }

        // ✅ Xử lý khi user gửi đánh giá số sao
        [HttpPost]
        public async Task<IActionResult> SubmitRating(int bookId, int stars)
        {
            if (stars < 1 || stars > 5)
                return BadRequest("Số sao không hợp lệ (chỉ từ 1 đến 5).");

            var userId = _userManager.GetUserId(User);

            // Quy đổi số sao → điểm Score
            int rating = stars switch
            {
                1 => -10,
                2 => -5,
                3 => 0,
                4 => 5,
                5 => 10,
                _ => 0
            };

            var existingInteraction = await _context.UserInteractions
                .FirstOrDefaultAsync(x => x.UserId == userId && x.BookId == bookId);

            if (existingInteraction == null)
            {
                // Nếu chưa có thì thêm mới (chỉ set rating)
                var interaction = new UserInteraction
                {
                    UserId = userId,
                    BookId = bookId,
                    Rating = rating,
                    InteractionDate = DateTime.Now
                };
                _context.UserInteractions.Add(interaction);
            }
            else
            {
                // Nếu đã có thì cộng dồn rating
                existingInteraction.Rating = (existingInteraction.Rating ?? 0) + rating;
                existingInteraction.InteractionDate = DateTime.Now;
                _context.UserInteractions.Update(existingInteraction);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(UserOrders));
        }
    }
}
