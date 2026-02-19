using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;

namespace TallinkFinance.Pages.Events.People;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;

    public CreateModel(AppDbContext context)
    {
        _context = context;
    }

    public EventHeader? EventItem { get; private set; }

    [BindProperty]
    public TallinkFinance.Models.Person PersonInput { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int eventId)
    {
        EventItem = await LoadEventAsync(eventId);
        if (EventItem is null)
        {
            return NotFound();
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

        PersonInput.EventId = eventId;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var duplicateExists = await _context.People
            .AnyAsync(person => person.EventId == eventId && person.Name.ToLower() == PersonInput.Name.ToLower());

        if (duplicateExists)
        {
            ModelState.AddModelError("PersonInput.Name", "A person with this name already exists in the event.");
            return Page();
        }

        _context.People.Add(PersonInput);
        await _context.SaveChangesAsync();
        return RedirectToPage("/Events/People/Index", new { eventId });
    }

    private Task<EventHeader?> LoadEventAsync(int eventId)
    {
        return _context.Events
            .AsNoTracking()
            .Where(@event => @event.Id == eventId)
            .Select(@event => new EventHeader
            {
                Id = @event.Id,
                Name = @event.Name
            })
            .FirstOrDefaultAsync();
    }

    public sealed class EventHeader
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
