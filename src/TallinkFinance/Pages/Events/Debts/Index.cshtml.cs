using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;
using TallinkFinance.Services;

namespace TallinkFinance.Pages.Events.Debts;

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
    public List<DebtRow> Debts { get; private set; } = new();

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

        Debts = (await _balances.GetDebtsAsync(eventId, includePayments))
            .Select(debt => new DebtRow
            {
                DebtorId = debt.DebtorId,
                CreditorId = debt.CreditorId,
                DebtorName = people.GetValueOrDefault(debt.DebtorId, "Unknown"),
                CreditorName = people.GetValueOrDefault(debt.CreditorId, "Unknown"),
                Amount = debt.Amount
            })
            .OrderByDescending(debt => debt.Amount)
            .ThenBy(debt => debt.DebtorName)
            .ThenBy(debt => debt.CreditorName)
            .ToList();

        return Page();
    }

    public sealed class EventHeader
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Currency { get; init; } = "EUR";
    }

    public sealed class DebtRow
    {
        public int DebtorId { get; init; }
        public int CreditorId { get; init; }
        public string DebtorName { get; init; } = string.Empty;
        public string CreditorName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
    }
}
