using BookShoppingCartMvcUI.Models;
using BookShoppingCartMvcUI.Models.DTOs;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Diagnostics;
using static System.Reflection.Metadata.BlobBuilder;

namespace BookShoppingCartMvcUI.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IHomeRepository _homeRepository;

        public HomeController(ILogger<HomeController> logger, IHomeRepository homeRepository)
        {
            _homeRepository = homeRepository;
            _logger = logger;
        }
        //Index() là action mặc định khi vào trang chủ(/).
        //Nó nhận 2 tham số:
        //sterm: chuỗi tìm kiếm
        //genreId: lọc theo thể loại
        public async Task<IActionResult> Index(string sterm = "", int genreId = 0)
        {
            //Gọi hàm GetBooks để lấy danh sách sách theo từ khóa sterm và thể loại genreId.
            IEnumerable<Book> books = await _homeRepository.GetBooks(sterm, genreId);
            //Lấy danh sách thể loại để hiển thị dropdown filter trên trang.
            IEnumerable<Genre> genres = await _homeRepository.Genres();
            //Tạo ViewModel và truyền về View
            //BookDisplayModel là 1 DTO dùng để gom nhiều dữ liệu lại(Books, Genres, từ khóa...) → truyền qua View.
            BookDisplayModel bookModel = new BookDisplayModel
            {
                Books = books,
                Genres = genres,
                STerm = sterm,
                GenreId = genreId
            };
            return View(bookModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult Contact()
        {
            return View();
        }
        public IActionResult About()
        {
            return View();
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}