using Microsoft.EntityFrameworkCore;

namespace BookShoppingCartMvcUI.Repositories
{
    public interface IBookRepository
    {
        Task AddBook(Book book);
        Task DeleteBook(Book book);
        Task<Book?> GetBookById(int id);
        Task<IEnumerable<Book>> GetBooks();
        Task<IEnumerable<Book>> GetBooksByGenreId(int genreId);
        Task UpdateBook(Book book);
    }

    public class BookRepository : IBookRepository
    {
        private readonly ApplicationDbContext _context;
        public BookRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddBook(Book book)
        {
            _context.Books.Add(book);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateBook(Book book)
        {
            _context.Books.Update(book);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteBook(Book book)
        {
            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
        }

        public async Task<Book?> GetBookById(int id)
        {
            return await _context.Books
                .Include(b => b.Genre)
                .FirstOrDefaultAsync(b => b.Id == id);
        }


        public async Task<IEnumerable<Book>> GetBooks() => await _context.Books.Include(a => a.Genre).ToListAsync();
        public async Task<IEnumerable<Book>> GetBooksByGenreId(int genreId)
        {
            return await _context.Books
                .Include(b => b.Genre)
                .Where(b => b.GenreId == genreId)
                .ToListAsync();
        }


    }
}
