using BookShoppingCartMvcUI.Models.DTOs;
using BookShoppingCartMvcUI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BookShoppingCartMvcUI.Controllers
{
    public class RecommendationController : Controller
    {
        private readonly RecommendationService _recommendationService;
        private readonly IBookRepository _bookRepository;
        private readonly UserManager<IdentityUser> _userManager;

        public RecommendationController(
            RecommendationService recommendationService,
            IBookRepository bookRepository,
            UserManager<IdentityUser> userManager)
        {
            _recommendationService = recommendationService;
            _bookRepository = bookRepository;
            _userManager = userManager;
        }

        private string? GetCurrentUserId()
            => _userManager.GetUserId(User);

        private async Task<IEnumerable<BookDTO>> MapRecommendationsAsync(IEnumerable<int> bookIds)
        {
            var result = new List<BookDTO>();
            foreach (var id in bookIds)
            {
                var book = await _bookRepository.GetBookById(id);
                if (book != null)
                {
                    result.Add(new BookDTO
                    {
                        Id = book.Id,
                        BookName = book.BookName,
                        AuthorName = book.AuthorName,
                        Price = book.Price,
                        Image = book.Image,
                        GenreId = book.GenreId,
                        GenreName = book.Genre?.GenreName,
                        Description = book.Description
                    });
                }
            }
            return result;
        }

        // ================= UserCF =================
        [HttpGet]
        public async Task<IActionResult> UserCF(int topN = 5, double alpha = 0.6)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Bạn cần đăng nhập để xem gợi ý.");

            var result = await _recommendationService.GetUserCFAsync(userId, topN, alpha);
            var books = await MapRecommendationsAsync(result.Recommendations.Select(r => r.BookId));

            return Json(books);
        }

        // ================= ItemCF =================
        [HttpGet]
        public async Task<IActionResult> ItemCF(int topN = 5, double alpha = 0.6)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Bạn cần đăng nhập để xem gợi ý.");

            var result = await _recommendationService.GetItemCFAsync(userId, topN, alpha);
            var books = await MapRecommendationsAsync(result.Recommendations.Select(r => r.BookId));

            return Json(books);
        }

        // ================= MF =================
        [HttpGet]
        public async Task<IActionResult> MF(int topN = 5)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Bạn cần đăng nhập để xem gợi ý.");

            var result = await _recommendationService.GetMFAsync(userId, topN);
            var books = await MapRecommendationsAsync(result.Recommendations.Select(r => r.BookId));

            return Json(books);
        }

        // =================  (mặc định) =================
        [HttpGet]
        public async Task<IActionResult> Best(int topN = 5)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Bạn cần đăng nhập để xem gợi ý.");

            // Gọi API 
            var result = await _recommendationService.GetBestModelAsync(userId, topN);
            var books = await MapRecommendationsAsync(result.Recommendations.Select(r => r.BookId));

            return Json(books);
        }
    }
}
