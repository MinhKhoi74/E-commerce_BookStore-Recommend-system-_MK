using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookShoppingCartMvcUI.Models;
using BookShoppingCartMvcUI.Models.DTOs;
using BookShoppingCartMvcUI.Services;
using Microsoft.AspNetCore.Identity;

namespace BookShoppingCartMvcUI.Controllers
{
    public class MainController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RecommendationService _recommendationService;
        private readonly IBookRepository _bookRepository;
        private readonly UserManager<IdentityUser> _userManager;

        public MainController(
            ApplicationDbContext context,
            RecommendationService recommendationService,
            IBookRepository bookRepository,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _recommendationService = recommendationService;
            _bookRepository = bookRepository;
            _userManager = userManager;
        }

        private string? GetCurrentUserId()
        {
            return _userManager.GetUserId(User);
        }

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

        public async Task<IActionResult> Index()
        {
            // ===== Load books by genre =====
            var genres = await _context.Genres.ToListAsync();
            var sections = new List<BooksByGenreSectionModel>();

            foreach (var genre in genres)
            {
                var books = await _context.Books
                    .Where(b => b.GenreId == genre.Id)
                    .Take(10)
                    .Select(b => new BookDTO
                    {
                        Id = b.Id,
                        BookName = b.BookName,
                        AuthorName = b.AuthorName,
                        Price = b.Price,
                        Image = b.Image,
                        GenreId = b.GenreId,
                        GenreName = genre.GenreName
                    })
                    .ToListAsync();

                if (books.Any())
                {
                    sections.Add(new BooksByGenreSectionModel
                    {
                        GenreId = genre.Id,
                        GenreName = genre.GenreName,
                        Books = books
                    });
                }
            }

            // ===== Load recommended books (best mặc định) =====
            var userId = GetCurrentUserId();
            if (userId != null)
            {
                var recResult = await _recommendationService.GetBestModelAsync(userId, topN: 10);
                var bookIds = recResult.Recommendations.Select(r => r.BookId);
                var recommendedBooks = await MapRecommendationsAsync(bookIds);

                ViewBag.RecommendedBooks = recommendedBooks;
            }

            return View(sections);
        }
    }
}
