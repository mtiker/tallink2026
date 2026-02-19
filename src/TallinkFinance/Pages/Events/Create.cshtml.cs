using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallinkFinance.Data;

namespace TallinkFinance.Pages.Events;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;

    public CreateModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public TallinkFinance.Models.Event EventInput { get; set; } = new()
    {
        StartDate = DateTime.Today,
        Currency = "EUR"
    };

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        EventInput.Currency = NormalizeCurrency(EventInput.Currency);

        if (EventInput.EndDate.HasValue && EventInput.EndDate.Value.Date < EventInput.StartDate.Date)
        {
            ModelState.AddModelError("EventInput.EndDate", "End date must be on or after start date.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        _context.Events.Add(EventInput);
        await _context.SaveChangesAsync();

        return RedirectToPage("/Events/Details", new { id = EventInput.Id });
    }

    private static string NormalizeCurrency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "EUR";
        }

        return value.Trim().ToUpperInvariant();
    }
}
