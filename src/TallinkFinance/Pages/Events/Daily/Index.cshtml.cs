using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;

namespace TallinkFinance.Pages.Events.Daily;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public EventHeader? EventItem { get; private set; }
    public DateTime? DateFrom { get; private set; }
    public DateTime? DateTo { get; private set; }
    public List<DayGroup> Days { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int eventId, DateTime? dateFrom, DateTime? dateTo)
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

        var people = await _context.People
            .AsNoTracking()
            .Where(person => person.EventId == eventId)
            .Select(person => new { person.Id, person.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name);

        var expenses = await _context.Expenses
            .AsNoTracking()
            .Where(expense => expense.EventId == eventId)
            .Select(expense => new ExpenseData
            {
                Id = expense.Id,
                Date = expense.Date,
                Title = expense.Title,
                Amount = expense.Amount,
                PayerId = expense.PaidByPersonId,
                PayerName = expense.PaidByPerson == null ? "Unknown" : expense.PaidByPerson.Name
            })
            .ToListAsync();

        var shares = await _context.ExpenseShares
            .AsNoTracking()
            .Where(share => share.Expense != null && share.Expense.EventId == eventId)
            .Select(share => new ExpenseShareData
            {
                ExpenseId = share.ExpenseId,
                PersonId = share.PersonId,
                ShareAmount = share.ShareAmount
            })
            .ToListAsync();

        var payments = await _context.Payments
            .AsNoTracking()
            .Where(payment => payment.EventId == eventId)
            .Select(payment => new PaymentData
            {
                Date = payment.Date,
                Amount = payment.Amount,
                FromPersonId = payment.FromPersonId,
                ToPersonId = payment.ToPersonId,
                FromName = payment.FromPerson == null ? "Unknown" : payment.FromPerson.Name,
                ToName = payment.ToPerson == null ? "Unknown" : payment.ToPerson.Name,
                Note = payment.Note
            })
            .ToListAsync();

        if (DateFrom.HasValue)
        {
            expenses = expenses.Where(expense => expense.Date.Date >= DateFrom.Value).ToList();
            payments = payments.Where(payment => payment.Date.Date >= DateFrom.Value).ToList();
        }

        if (DateTo.HasValue)
        {
            expenses = expenses.Where(expense => expense.Date.Date <= DateTo.Value).ToList();
            payments = payments.Where(payment => payment.Date.Date <= DateTo.Value).ToList();
        }

        var sharesByExpenseId = shares
            .GroupBy(share => share.ExpenseId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var allDates = expenses
            .Select(expense => expense.Date.Date)
            .Concat(payments.Select(payment => payment.Date.Date))
            .Distinct()
            .OrderByDescending(date => date)
            .ToList();

        Days = allDates
            .Select(date =>
            {
                var dayExpenses = expenses
                    .Where(expense => expense.Date.Date == date)
                    .OrderBy(expense => expense.Id)
                    .ToList();
                var dayPayments = payments
                    .Where(payment => payment.Date.Date == date)
                    .OrderBy(payment => payment.FromName)
                    .ThenBy(payment => payment.ToName)
                    .ToList();

                var deltaByPersonId = new Dictionary<int, decimal>();

                foreach (var expense in dayExpenses)
                {
                    if (!sharesByExpenseId.TryGetValue(expense.Id, out var expenseShares))
                    {
                        continue;
                    }

                    foreach (var share in expenseShares)
                    {
                        if (share.PersonId == expense.PayerId)
                        {
                            continue;
                        }

                        AddDelta(deltaByPersonId, share.PersonId, -share.ShareAmount);
                        AddDelta(deltaByPersonId, expense.PayerId, share.ShareAmount);
                    }
                }

                foreach (var payment in dayPayments)
                {
                    AddDelta(deltaByPersonId, payment.FromPersonId, payment.Amount);
                    AddDelta(deltaByPersonId, payment.ToPersonId, -payment.Amount);
                }

                return new DayGroup
                {
                    Date = date,
                    ExpenseTotal = dayExpenses.Sum(expense => expense.Amount),
                    PaymentTotal = dayPayments.Sum(payment => payment.Amount),
                    Expenses = dayExpenses.Select(expense => new DayExpenseRow
                    {
                        ExpenseId = expense.Id,
                        Title = expense.Title,
                        Amount = expense.Amount,
                        PayerName = expense.PayerName
                    }).ToList(),
                    Payments = dayPayments.Select(payment => new DayPaymentRow
                    {
                        FromName = payment.FromName,
                        ToName = payment.ToName,
                        Amount = payment.Amount,
                        Note = payment.Note
                    }).ToList(),
                    Changes = deltaByPersonId
                        .Where(item => decimal.Abs(item.Value) > 0.0001m)
                        .Select(item => new PersonChangeRow
                        {
                            PersonName = people.GetValueOrDefault(item.Key, "Unknown"),
                            Delta = decimal.Round(item.Value, 2)
                        })
                        .OrderByDescending(item => decimal.Abs(item.Delta))
                        .ThenBy(item => item.PersonName)
                        .ToList()
                };
            })
            .ToList();

        return Page();
    }

    private static void AddDelta(IDictionary<int, decimal> deltas, int personId, decimal delta)
    {
        if (decimal.Abs(delta) < 0.0001m)
        {
            return;
        }

        if (deltas.TryGetValue(personId, out var current))
        {
            deltas[personId] = current + delta;
            return;
        }

        deltas[personId] = delta;
    }

    public sealed class EventHeader
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Currency { get; init; } = "EUR";
    }

    public sealed class DayGroup
    {
        public DateTime Date { get; init; }
        public decimal ExpenseTotal { get; init; }
        public decimal PaymentTotal { get; init; }
        public List<DayExpenseRow> Expenses { get; init; } = new();
        public List<DayPaymentRow> Payments { get; init; } = new();
        public List<PersonChangeRow> Changes { get; init; } = new();
    }

    public sealed class DayExpenseRow
    {
        public int ExpenseId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string PayerName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
    }

    public sealed class DayPaymentRow
    {
        public string FromName { get; init; } = string.Empty;
        public string ToName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string? Note { get; init; }
    }

    public sealed class PersonChangeRow
    {
        public string PersonName { get; init; } = string.Empty;
        public decimal Delta { get; init; }
    }

    private sealed class ExpenseData
    {
        public int Id { get; init; }
        public DateTime Date { get; init; }
        public string Title { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public int PayerId { get; init; }
        public string PayerName { get; init; } = string.Empty;
    }

    private sealed class ExpenseShareData
    {
        public int ExpenseId { get; init; }
        public int PersonId { get; init; }
        public decimal ShareAmount { get; init; }
    }

    private sealed class PaymentData
    {
        public DateTime Date { get; init; }
        public decimal Amount { get; init; }
        public int FromPersonId { get; init; }
        public int ToPersonId { get; init; }
        public string FromName { get; init; } = string.Empty;
        public string ToName { get; init; } = string.Empty;
        public string? Note { get; init; }
    }
}
