using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;
using TallinkFinance.Services;

namespace TallinkFinance.Pages.Events.People;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly BalanceCalculatorService _balances;

    public DetailsModel(AppDbContext context, BalanceCalculatorService balances)
    {
        _context = context;
        _balances = balances;
    }

    public EventHeader? EventItem { get; private set; }
    public PersonProfile? PersonItem { get; private set; }

    public List<PaidExpenseRow> PaidExpenses { get; private set; } = new();
    public List<ParticipantExpenseRow> ParticipantExpenses { get; private set; } = new();
    public List<DebtRow> OwesRows { get; private set; } = new();
    public List<DebtRow> IsOwedRows { get; private set; } = new();
    public List<PaymentRow> OutgoingPayments { get; private set; } = new();
    public List<PaymentRow> IncomingPayments { get; private set; } = new();

    public decimal PaidTotal => PaidExpenses.Sum(expense => expense.Amount);
    public decimal ParticipatedTotal => ParticipantExpenses.Sum(expense => expense.ShareAmount);
    public decimal OwesTotal => OwesRows.Sum(debt => debt.Amount);
    public decimal IsOwedTotal => IsOwedRows.Sum(debt => debt.Amount);
    public decimal Net => IsOwedTotal - OwesTotal;

    public async Task<IActionResult> OnGetAsync(int eventId, int personId)
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

        PersonItem = await _context.People
            .AsNoTracking()
            .Where(person => person.EventId == eventId && person.Id == personId)
            .Select(person => new PersonProfile
            {
                Id = person.Id,
                Name = person.Name,
                Contact = person.Contact
            })
            .FirstOrDefaultAsync();

        if (PersonItem is null)
        {
            return NotFound();
        }

        var peopleNameMap = await _context.People
            .AsNoTracking()
            .Where(person => person.EventId == eventId)
            .Select(person => new { person.Id, person.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name);

        PaidExpenses = await _context.Expenses
            .AsNoTracking()
            .Where(expense => expense.EventId == eventId && expense.PaidByPersonId == personId)
            .OrderByDescending(expense => expense.Date)
            .ThenByDescending(expense => expense.Id)
            .Select(expense => new PaidExpenseRow
            {
                ExpenseId = expense.Id,
                Date = expense.Date,
                Title = expense.Title,
                Amount = expense.Amount
            })
            .ToListAsync();

        ParticipantExpenses = await _context.ExpenseShares
            .AsNoTracking()
            .Where(share => share.PersonId == personId && share.Expense != null && share.Expense.EventId == eventId)
            .OrderByDescending(share => share.Expense!.Date)
            .ThenByDescending(share => share.ExpenseId)
            .Select(share => new ParticipantExpenseRow
            {
                ExpenseId = share.ExpenseId,
                Date = share.Expense!.Date,
                Title = share.Expense.Title,
                ShareAmount = share.ShareAmount,
                PaidByName = share.Expense.PaidByPerson == null ? "Unknown" : share.Expense.PaidByPerson.Name
            })
            .ToListAsync();

        var debts = await _balances.GetDebtsAsync(eventId, includePayments: true);
        OwesRows = debts
            .Where(debt => debt.DebtorId == personId)
            .Select(debt => new DebtRow
            {
                OtherPersonName = peopleNameMap.GetValueOrDefault(debt.CreditorId, "Unknown"),
                Amount = debt.Amount
            })
            .OrderByDescending(debt => debt.Amount)
            .ThenBy(debt => debt.OtherPersonName)
            .ToList();

        IsOwedRows = debts
            .Where(debt => debt.CreditorId == personId)
            .Select(debt => new DebtRow
            {
                OtherPersonName = peopleNameMap.GetValueOrDefault(debt.DebtorId, "Unknown"),
                Amount = debt.Amount
            })
            .OrderByDescending(debt => debt.Amount)
            .ThenBy(debt => debt.OtherPersonName)
            .ToList();

        OutgoingPayments = await _context.Payments
            .AsNoTracking()
            .Where(payment => payment.EventId == eventId && payment.FromPersonId == personId)
            .OrderByDescending(payment => payment.Date)
            .ThenByDescending(payment => payment.Id)
            .Select(payment => new PaymentRow
            {
                Date = payment.Date,
                OtherPersonName = payment.ToPerson == null ? "Unknown" : payment.ToPerson.Name,
                Amount = payment.Amount,
                Note = payment.Note
            })
            .ToListAsync();

        IncomingPayments = await _context.Payments
            .AsNoTracking()
            .Where(payment => payment.EventId == eventId && payment.ToPersonId == personId)
            .OrderByDescending(payment => payment.Date)
            .ThenByDescending(payment => payment.Id)
            .Select(payment => new PaymentRow
            {
                Date = payment.Date,
                OtherPersonName = payment.FromPerson == null ? "Unknown" : payment.FromPerson.Name,
                Amount = payment.Amount,
                Note = payment.Note
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

    public sealed class PersonProfile
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Contact { get; init; }
    }

    public sealed class PaidExpenseRow
    {
        public int ExpenseId { get; init; }
        public DateTime Date { get; init; }
        public string Title { get; init; } = string.Empty;
        public decimal Amount { get; init; }
    }

    public sealed class ParticipantExpenseRow
    {
        public int ExpenseId { get; init; }
        public DateTime Date { get; init; }
        public string Title { get; init; } = string.Empty;
        public decimal ShareAmount { get; init; }
        public string PaidByName { get; init; } = string.Empty;
    }

    public sealed class DebtRow
    {
        public string OtherPersonName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
    }

    public sealed class PaymentRow
    {
        public DateTime Date { get; init; }
        public string OtherPersonName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string? Note { get; init; }
    }
}
