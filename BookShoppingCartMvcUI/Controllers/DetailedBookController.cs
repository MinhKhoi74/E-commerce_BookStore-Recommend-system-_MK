using BookShoppingCartMvcUI.Data;
using BookShoppingCartMvcUI.Models;
using BookShoppingCartMvcUI.Models.DTOs;
using BookShoppingCartMvcUI.Services;
using BookShoppingCartMvcUI.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShoppingCartMvcUI.Controllers
{
    public class DetailedBookController : Controller
    {
        private readonly IBookRepository _bookRepo;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RecommendationService _recommendationService;

        public DetailedBookController(
            IBookRepository bookRepo,
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            RecommendationService recommendationService)
        {
            _bookRepo = bookRepo;
            _context = context;
            _userManager = userManager;
            _recommendationService = recommendationService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Details(int id)
        {
            var book = await _bookRepo.GetBookById(id);
            if (book == null)
            {
                return NotFound();
            }

            // ✅ Ghi nhận interaction (người dùng đã xem sách này)
            if (User.Identity.IsAuthenticated)
            {
                var userId = _userManager.GetUserId(User);

                var existing = await _context.UserInteractions
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.BookId == id);

                if (existing == null)
                {
                    var interaction = new UserInteraction
                    {
                        UserId = userId,
                        BookId = id,
                        Score = 1, // View = 1
                        InteractionDate = DateTime.Now
                    };
                    _context.UserInteractions.Add(interaction);
                }
                else
                {
                    existing.Score = (existing.Score ?? 0) + 1;
                    existing.InteractionDate = DateTime.Now;
                    _context.UserInteractions.Update(existing);
                }

                await _context.SaveChangesAsync();

                // ✅ Gợi ý UserCF
                var usercfResult = await _recommendationService.GetUserCFAsync(userId, topN: 10);
                var usercfBooks = await MapRecommendationsAsync(usercfResult.Recommendations.Select(r => r.BookId));
                ViewBag.UserCFRecommendations = usercfBooks;

                // ✅ Gợi ý ItemCF
                var itemcfResult = await _recommendationService.GetItemCFAsync(userId, topN: 10);
                var itemcfBooks = await MapRecommendationsAsync(itemcfResult.Recommendations.Select(r => r.BookId));
                ViewBag.ItemCFRecommendations = itemcfBooks;
            }

            // ✅ Lấy sách liên quan theo thể loại
            var relatedBooks = await _bookRepo.GetBooksByGenreId(book.GenreId);
            var relatedDTOs = relatedBooks
                .Where(b => b.Id != book.Id)
                .Select(b => new BookDTO
                {
                    Id = b.Id,
                    BookName = b.BookName,
                    AuthorName = b.AuthorName,
                    Price = b.Price,
                    Image = b.Image,
                    GenreId = b.GenreId,
                    GenreName = b.Genre?.GenreName,
                    Description = b.Description
                }).ToList();

            ViewBag.RelatedBooks = new BooksByGenreSectionModel
            {
                GenreId = book.GenreId,
                GenreName = book.Genre?.GenreName ?? "Sản phẩm liên quan",
                Books = relatedDTOs
            };

            return View(book);
        }

        // 🔹 Helper: map danh sách BookId -> BookDTO
        private async Task<IEnumerable<BookDTO>> MapRecommendationsAsync(IEnumerable<int> bookIds)
        {
            var result = new List<BookDTO>();
            foreach (var id in bookIds)
            {
                var book = await _bookRepo.GetBookById(id);
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
    }
}
