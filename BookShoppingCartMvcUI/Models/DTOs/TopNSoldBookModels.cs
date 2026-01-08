namespace BookShoppingCartMvcUI.Models.DTOs;

public record TopNSoldBookModel(
    int BookId,
    string BookName,
    string AuthorName,
    string Image,
    int TotalUnitSold);

public record TopNSoldBooksVm(
    DateTime StartDate,
    DateTime EndDate,
    IEnumerable<TopNSoldBookModel> TopNSoldBooks);
