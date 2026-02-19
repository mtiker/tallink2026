using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;
using TallinkFinance.Services;

namespace TallinkFinance.Pages.Events.Balances;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly BalanceCalculatorService _balances;

    public IndexModel(AppDbContext context, BalanceCalculatorService balances)
    {
        _context = context;
        _balances = balances;
    }

    public EventHeader? EventItem { get; private set; }
    public bool IncludePayments { get; private set; } = true;
    public List<BalanceRow> Rows { get; private set; } = new();
    public decimal NetControl => Rows.Sum(row => row.Net);

    public async Task<IActionResult> OnGetAsync(int eventId, bool includePayments = true)
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

        IncludePayments = includePayments;

        var people = await _context.People
            .AsNoTracking()
            .Where(person => person.EventId == eventId)
            .Select(person => new { person.Id, person.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name);

        Rows = (await _balances.GetBalancesAsync(eventId, includePayments))
            .Select(balance => new BalanceRow
            {
                PersonId = balance.PersonId,
                PersonName = people.GetValueOrDefault(balance.PersonId, "Unknown"),
                OwesTotal = balance.OwesTotal,
                IsOwedTotal = balance.IsOwedTotal
            })
            .OrderByDescending(row => row.Net)
            .ThenBy(row => row.PersonName)
            .ToList();

        return Page();
    }

    public sealed class EventHeader
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Currency { get; init; } = "EUR";
    }

    public sealed class BalanceRow
    {
        public int PersonId { get; init; }
        public string PersonName { get; init; } = string.Empty;
        public decimal OwesTotal { get; init; }
        public decimal IsOwedTotal { get; init; }
        public decimal Net => IsOwedTotal - OwesTotal;
    }
}
