using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;

namespace TallinkFinance.Pages.Events.Expenses;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public EventHeader? EventItem { get; private set; }
    public List<ExpenseRow> Expenses { get; private set; } = new();
    public List<SelectListItem> PayerOptions { get; private set; } = new();

    public DateTime? DateFrom { get; private set; }
    public DateTime? DateTo { get; private set; }
    public int? PayerId { get; private set; }
    public string? Search { get; private set; }

    public async Task<IActionResult> OnGetAsync(int eventId, DateTime? dateFrom, DateTime? dateTo, int? payerId, string? search)
    {
        EventItem = await _context.Events
            .AsNoTracking()
            .Where(@event => @event.Id == eventId)
            .Select(@event => new EventHeader
            {
                Id = @event.Id,
                Name = @event.Name,
                Currency = @event.Currency.ToUpper()
            })
            .FirstOrDefaultAsync();

        if (EventItem is null)
        {
            return NotFound();
        }

        DateFrom = dateFrom?.Date;
        DateTo = dateTo?.Date;
        PayerId = payerId;
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var people = await _context.People
            .AsNoTracking()
            .Where(person => person.EventId == eventId)
            .OrderBy(person => person.Name)
            .Select(person => new { person.Id, person.Name })
            .ToListAsync();

        PayerOptions = people
            .Select(person => new SelectListItem
            {
                Value = person.Id.ToString(),
                Text = person.Name,
                Selected = PayerId == person.Id
            })
            .ToList();

        var query = _context.Expenses
            .AsNoTracking()
            .Where(expense => expense.EventId == eventId);

        if (DateFrom.HasValue)
        {
            query = query.Where(expense => expense.Date.Date >= DateFrom.Value);
        }

        if (DateTo.HasValue)
        {
            query = query.Where(expense => expense.Date.Date <= DateTo.Value);
        }

        if (PayerId.HasValue)
        {
            query = query.Where(expense => expense.PaidByPersonId == PayerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = $"%{Search}%";
            query = query.Where(expense =>
                EF.Functions.Like(expense.Title, term)
                || (expense.Category != null && EF.Functions.Like(expense.Category, term)));
        }

        Expenses = await query
            .OrderByDescending(expense => expense.Date)
            .ThenByDescending(expense => expense.Id)
            .Select(expense => new ExpenseRow
            {
                Id = expense.Id,
                Date = expense.Date,
                Title = expense.Title,
                Amount = expense.Amount,
                Category = expense.Category,
                PayerName = expense.PaidByPerson == null ? "Unknown" : expense.PaidByPerson.Name,
                ParticipantCount = expense.Shares.Count
            })
            .ToListAsync();

        return Page();
    }

    public sealed class EventHeader
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Currency { get; init; } = "EUR";
    }

    public sealed class ExpenseRow
    {
        public int Id { get; init; }
        public DateTime Date { get; init; }
        public string Title { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string? Category { get; init; }
        public string PayerName { get; init; } = string.Empty;
        public int ParticipantCount { get; init; }
    }
}
