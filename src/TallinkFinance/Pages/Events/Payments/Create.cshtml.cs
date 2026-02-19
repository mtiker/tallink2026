using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;
using TallinkFinance.Models;

namespace TallinkFinance.Pages.Events.Payments;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;

    public CreateModel(AppDbContext context)
    {
        _context = context;
    }

    public EventHeader? EventItem { get; private set; }
    public List<PersonOption> PersonOptions { get; private set; } = new();

    [BindProperty]
    public PaymentInputModel Input { get; set; } = new()
    {
        Date = DateTime.Today
    };

    public async Task<IActionResult> OnGetAsync(int eventId)
    {
        EventItem = await LoadEventAsync(eventId);
        if (EventItem is null)
        {
            return NotFound();
        }

        await LoadPeopleAsync(eventId);
        if (PersonOptions.Any())
        {
            Input.FromPersonId = PersonOptions[0].Id;
            Input.ToPersonId = PersonOptions.Count > 1 ? PersonOptions[1].Id : PersonOptions[0].Id;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int eventId)
    {
        EventItem = await LoadEventAsync(eventId);
        if (EventItem is null)
        {
            return NotFound();
        }

        await LoadPeopleAsync(eventId);

        if (!PersonOptions.Any(person => person.Id == Input.FromPersonId))
        {
            ModelState.AddModelError("Input.FromPersonId", "Select a valid sender.");
        }

        if (!PersonOptions.Any(person => person.Id == Input.ToPersonId))
        {
            ModelState.AddModelError("Input.ToPersonId", "Select a valid receiver.");
        }

        if (Input.FromPersonId == Input.ToPersonId)
        {
            ModelState.AddModelError(string.Empty, "Sender and receiver must be different.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var payment = new Payment
        {
            EventId = eventId,
            FromPersonId = Input.FromPersonId,
            ToPersonId = Input.ToPersonId,
            Amount = decimal.Round(Input.Amount, 2),
            Date = Input.Date.Date,
            Note = string.IsNullOrWhiteSpace(Input.Note) ? null : Input.Note.Trim()
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        return RedirectToPage("/Events/Payments/Index", new { eventId });
    }

    private Task<EventHeader?> LoadEventAsync(int eventId)
    {
        return _context.Events
            .AsNoTracking()
            .Where(@event => @event.Id == eventId)
            .Select(@event => new EventHeader
            {
                Id = @event.Id,
                Name = @event.Name,
                Currency = @event.Currency.ToUpper()
            })
            .FirstOrDefaultAsync();
    }

    private async Task LoadPeopleAsync(int eventId)
    {
        PersonOptions = await _context.People
            .AsNoTracking()
            .Where(person => person.EventId == eventId)
            .OrderBy(person => person.Name)
            .Select(person => new PersonOption
            {
                Id = person.Id,
                Name = person.Name
            })
            .ToListAsync();
    }

    public sealed class EventHeader
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Currency { get; init; } = "EUR";
    }

    public sealed class PersonOption
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public sealed class PaymentInputModel
    {
        public int FromPersonId { get; set; }
        public int ToPersonId { get; set; }

        [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [StringLength(300)]
        public string? Note { get; set; }
    }
}
