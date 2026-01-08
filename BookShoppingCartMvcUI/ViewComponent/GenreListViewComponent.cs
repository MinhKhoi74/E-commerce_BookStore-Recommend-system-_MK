using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class GenreListViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    public GenreListViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var genres = await _context.Genres
            .Select(g => new GenreDTO
            {
                Id = g.Id,
                GenreName = g.GenreName,
                ThumbnailImage = _context.Books
                    .Where(b => b.GenreId == g.Id)
                    .OrderBy(b => b.Id)
                    .Select(b => b.Image)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return View(genres);
    }
}
