using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;

namespace TallinkFinance.Pages.Events;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public List<EventListItem> Events { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Events = await _context.Events
            .AsNoTracking()
            .OrderByDescending(@event => @event.StartDate)
            .ThenBy(@event => @event.Name)
            .Select(@event => new EventListItem
            {
                Id = @event.Id,
                Name = @event.Name,
                StartDate = @event.StartDate,
                EndDate = @event.EndDate,
                Currency = @event.Currency.ToUpper(),
                PeopleCount = @event.People.Count,
                ExpenseTotal = @event.Expenses.Sum(expense => (decimal?)expense.Amount) ?? 0m,
                PaymentTotal = @event.Payments.Sum(payment => (decimal?)payment.Amount) ?? 0m
            })
            .ToListAsync();
    }

    public sealed class EventListItem
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public DateTime StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public string Currency { get; init; } = "EUR";
        public int PeopleCount { get; init; }
        public decimal ExpenseTotal { get; init; }
        public decimal PaymentTotal { get; init; }
    }
}
