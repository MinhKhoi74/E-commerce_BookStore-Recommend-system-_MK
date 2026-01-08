using BookShoppingCartMvcUI.Models.DTOs;
using BookShoppingCartMvcUI.Repositories;
using Microsoft.AspNetCore.Mvc;

public class HomeTopSellingBooksViewComponent : ViewComponent
{
    private readonly IReportRepository _reportRepository;

    public HomeTopSellingBooksViewComponent(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<IViewComponentResult> InvokeAsync(string view = "Default")
    {
        var data = await _reportRepository.GetTopNSellingBooksAllTime();
        return View(view, data);
    }
}

