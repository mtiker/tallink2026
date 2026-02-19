using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;

namespace TallinkFinance.Pages.Events.Expenses;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _context;

    public DetailsModel(AppDbContext context)
    {
        _context = context;
    }

    public EventHeader? EventItem { get; private set; }
    public ExpenseDetails? ExpenseItem { get; private set; }
    public List<ShareRow> Shares { get; private set; } = new();
    public List<DebtRow> DerivedDebts { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int eventId, int expenseId)
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

        ExpenseItem = await _context.Expenses
            .AsNoTracking()
            .Where(expense => expense.EventId == eventId && expense.Id == expenseId)
            .Select(expense => new ExpenseDetails
            {
                Id = expense.Id,
                Date = expense.Date,
                Title = expense.Title,
                Amount = expense.Amount,
                Category = expense.Category,
                PaidByPersonId = expense.PaidByPersonId,
                PaidByName = expense.PaidByPerson == null ? "Unknown" : expense.PaidByPerson.Name
            })
            .FirstOrDefaultAsync();

        if (ExpenseItem is null)
        {
            return NotFound();
        }

        Shares = await _context.ExpenseShares
            .AsNoTracking()
            .Where(share => share.ExpenseId == expenseId)
            .OrderBy(share => share.Person == null ? string.Empty : share.Person.Name)
            .Select(share => new ShareRow
            {
                PersonId = share.PersonId,
                PersonName = share.Person == null ? "Unknown" : share.Person.Name,
                ShareAmount = share.ShareAmount
            })
            .ToListAsync();

        DerivedDebts = Shares
            .Where(share => share.PersonId != ExpenseItem.PaidByPersonId)
            .Select(share => new DebtRow
            {
                DebtorName = share.PersonName,
                CreditorName = ExpenseItem.PaidByName,
                Amount = share.ShareAmount
            })
            .OrderByDescending(debt => debt.Amount)
            .ThenBy(debt => debt.DebtorName)
            .ToList();

        return Page();
    }

    public sealed class EventHeader
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Currency { get; init; } = "EUR";
    }

    public sealed class ExpenseDetails
    {
        public int Id { get; init; }
        public DateTime Date { get; init; }
        public string Title { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string? Category { get; init; }
        public int PaidByPersonId { get; init; }
        public string PaidByName { get; init; } = string.Empty;
    }

    public sealed class ShareRow
    {
        public int PersonId { get; init; }
        public string PersonName { get; init; } = string.Empty;
        public decimal ShareAmount { get; init; }
    }

    public sealed class DebtRow
    {
        public string DebtorName { get; init; } = string.Empty;
        public string CreditorName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
    }
}
