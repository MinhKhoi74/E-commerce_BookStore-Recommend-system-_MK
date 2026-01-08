namespace BookShoppingCartMvcUI.Models.DTOs
{
    public class BooksByGenreSectionModel
    {
        public string GenreName { get; set; }
        public int GenreId { get; set; }
        public IEnumerable<BookDTO> Books { get; set; }
    }
}
