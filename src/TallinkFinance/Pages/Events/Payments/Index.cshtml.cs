using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;

namespace TallinkFinance.Pages.Events.Payments;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public EventHeader? EventItem { get; private set; }
    public List<SelectListItem> PeopleOptions { get; private set; } = new();
    public List<PaymentRow> Payments { get; private set; } = new();

    public DateTime? DateFrom { get; private set; }
    public DateTime? DateTo { get; private set; }
    public int? PersonId { get; private set; }

    public async Task<IActionResult> OnGetAsync(int eventId, DateTime? dateFrom, DateTime? dateTo, int? personId)
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
        PersonId = personId;

        var people = await _context.People
            .AsNoTracking()
            .Where(person => person.EventId == eventId)
            .OrderBy(person => person.Name)
            .Select(person => new { person.Id, person.Name })
            .ToListAsync();

        PeopleOptions = people
            .Select(person => new SelectListItem
            {
                Value = person.Id.ToString(),
                Text = person.Name,
                Selected = PersonId == person.Id
            })
            .ToList();

        var query = _context.Payments
            .AsNoTracking()
            .Where(payment => payment.EventId == eventId);

        if (DateFrom.HasValue)
        {
            query = query.Where(payment => payment.Date.Date >= DateFrom.Value);
        }

        if (DateTo.HasValue)
        {
            query = query.Where(payment => payment.Date.Date <= DateTo.Value);
        }

        if (PersonId.HasValue)
        {
            query = query.Where(payment => payment.FromPersonId == PersonId.Value || payment.ToPersonId == PersonId.Value);
        }

        Payments = await query
            .OrderByDescending(payment => payment.Date)
            .ThenByDescending(payment => payment.Id)
            .Select(payment => new PaymentRow
            {
                Date = payment.Date,
                FromName = payment.FromPerson == null ? "Unknown" : payment.FromPerson.Name,
                ToName = payment.ToPerson == null ? "Unknown" : payment.ToPerson.Name,
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

    public sealed class PaymentRow
    {
        public DateTime Date { get; init; }
        public string FromName { get; init; } = string.Empty;
        public string ToName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string? Note { get; init; }
    }
}
