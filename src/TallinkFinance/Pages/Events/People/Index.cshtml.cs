using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;
using TallinkFinance.Services;

namespace TallinkFinance.Pages.Events.People;

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
    public List<PersonRow> People { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int eventId)
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

        var people = await _context.People
            .AsNoTracking()
            .Where(person => person.EventId == eventId)
            .OrderBy(person => person.Name)
            .Select(person => new PersonRow
            {
                Id = person.Id,
                Name = person.Name,
                Contact = person.Contact
            })
            .ToListAsync();

        var paidTotals = await _context.Expenses
            .AsNoTracking()
            .Where(expense => expense.EventId == eventId)
            .GroupBy(expense => expense.PaidByPersonId)
            .Select(group => new
            {
                PersonId = group.Key,
                Total = group.Sum(expense => expense.Amount)
            })
            .ToDictionaryAsync(item => item.PersonId, item => item.Total);

        var shareTotals = await _context.ExpenseShares
            .AsNoTracking()
            .Where(share => share.Expense != null && share.Expense.EventId == eventId)
            .GroupBy(share => share.PersonId)
            .Select(group => new
            {
                PersonId = group.Key,
                Total = group.Sum(share => share.ShareAmount)
            })
            .ToDictionaryAsync(item => item.PersonId, item => item.Total);

        var balances = await _balances.GetBalancesAsync(eventId, includePayments: true);
        var balancesByPersonId = balances.ToDictionary(balance => balance.PersonId, balance => balance);

        foreach (var person in people)
        {
            if (paidTotals.TryGetValue(person.Id, out var paidTotal))
            {
                person.PaidTotal = paidTotal;
            }

            if (shareTotals.TryGetValue(person.Id, out var shareTotal))
            {
                person.ParticipatedTotal = shareTotal;
            }

            if (balancesByPersonId.TryGetValue(person.Id, out var balance))
            {
                person.OwesTotal = balance.OwesTotal;
                person.IsOwedTotal = balance.IsOwedTotal;
            }
        }

        People = people;
        return Page();
    }

    public sealed class EventHeader
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Currency { get; init; } = "EUR";
    }

    public sealed class PersonRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Contact { get; init; }
        public decimal PaidTotal { get; set; }
        public decimal ParticipatedTotal { get; set; }
        public decimal OwesTotal { get; set; }
        public decimal IsOwedTotal { get; set; }
        public decimal SettlementNet => IsOwedTotal - OwesTotal;
        public decimal Net => SettlementNet - PaidTotal;
    }
}
