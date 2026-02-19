using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;

namespace TallinkFinance.Pages.Events;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _context;

    public DetailsModel(AppDbContext context)
    {
        _context = context;
    }

    public EventDashboard? Dashboard { get; private set; }
    public List<RecentExpense> RecentExpenses { get; private set; } = new();
    public List<RecentPayment> RecentPayments { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Dashboard = await _context.Events
            .AsNoTracking()
            .Where(@event => @event.Id == id)
            .Select(@event => new EventDashboard
            {
                Id = @event.Id,
                Name = @event.Name,
                Currency = @event.Currency.ToUpper(),
                StartDate = @event.StartDate,
                EndDate = @event.EndDate,
                PeopleCount = @event.People.Count,
                ExpenseCount = @event.Expenses.Count,
                ExpenseTotal = @event.Expenses.Sum(expense => (decimal?)expense.Amount) ?? 0m,
                PaymentCount = @event.Payments.Count,
                PaymentTotal = @event.Payments.Sum(payment => (decimal?)payment.Amount) ?? 0m
            })
            .FirstOrDefaultAsync();

        if (Dashboard is null)
        {
            return NotFound();
        }

        RecentExpenses = await _context.Expenses
            .AsNoTracking()
            .Where(expense => expense.EventId == id)
            .OrderByDescending(expense => expense.Date)
            .ThenByDescending(expense => expense.Id)
            .Select(expense => new RecentExpense
            {
                Id = expense.Id,
                Date = expense.Date,
                Title = expense.Title,
                Amount = expense.Amount,
                PaidByName = expense.PaidByPerson == null ? "Unknown" : expense.PaidByPerson.Name
            })
            .Take(6)
            .ToListAsync();

        RecentPayments = await _context.Payments
            .AsNoTracking()
            .Where(payment => payment.EventId == id)
            .OrderByDescending(payment => payment.Date)
            .ThenByDescending(payment => payment.Id)
            .Select(payment => new RecentPayment
            {
                Date = payment.Date,
                Amount = payment.Amount,
                FromName = payment.FromPerson == null ? "Unknown" : payment.FromPerson.Name,
                ToName = payment.ToPerson == null ? "Unknown" : payment.ToPerson.Name
            })
            .Take(6)
            .ToListAsync();

        return Page();
    }

    public sealed class EventDashboard
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Currency { get; init; } = "EUR";
        public DateTime StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public int PeopleCount { get; init; }
        public int ExpenseCount { get; init; }
        public decimal ExpenseTotal { get; init; }
        public int PaymentCount { get; init; }
        public decimal PaymentTotal { get; init; }
    }

    public sealed class RecentExpense
    {
        public int Id { get; init; }
        public DateTime Date { get; init; }
        public string Title { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string PaidByName { get; init; } = string.Empty;
    }

    public sealed class RecentPayment
    {
        public DateTime Date { get; init; }
        public string FromName { get; init; } = string.Empty;
        public string ToName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
    }
}
