using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;
using TallinkFinance.Models;

namespace TallinkFinance.Pages.Events.Expenses;

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
    public ExpenseInputModel Input { get; set; } = new()
    {
        Date = DateTime.Today,
        SplitMode = SplitMode.Equal
    };

    [BindProperty]
    public List<ParticipantShareInput> ParticipantShares { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int eventId)
    {
        EventItem = await LoadEventAsync(eventId);
        if (EventItem is null)
        {
            return NotFound();
        }

        await LoadPeopleAsync(eventId);

        ParticipantShares = PersonOptions
            .Select(person => new ParticipantShareInput
            {
                PersonId = person.Id,
                PersonName = person.Name,
                IsSelected = true,
                Ratio = 1m
            })
            .ToList();

        if (PersonOptions.Any())
        {
            Input.PaidByPersonId = PersonOptions[0].Id;
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
        EnsureParticipantRows();

        if (!PersonOptions.Any())
        {
            ModelState.AddModelError(string.Empty, "Add at least one person before creating expenses.");
            return Page();
        }

        if (!PersonOptions.Any(person => person.Id == Input.PaidByPersonId))
        {
            ModelState.AddModelError("Input.PaidByPersonId", "Select a valid payer.");
        }

        var selected = ParticipantShares
            .Where(share => share.IsSelected)
            .ToList();

        if (!selected.Any())
        {
            ModelState.AddModelError(string.Empty, "Select at least one participant.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var computedShares = ComputeShares(selected);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var expense = new Expense
        {
            EventId = eventId,
            Title = Input.Title.Trim(),
            Date = Input.Date.Date,
            Amount = decimal.Round(Input.Amount, 2),
            Category = string.IsNullOrWhiteSpace(Input.Category) ? null : Input.Category.Trim(),
            PaidByPersonId = Input.PaidByPersonId,
            Shares = computedShares
                .Select(share => new ExpenseShare
                {
                    PersonId = share.PersonId,
                    ShareAmount = share.Amount
                })
                .ToList()
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        return RedirectToPage("/Events/Expenses/Details", new { eventId, expenseId = expense.Id });
    }

    private List<ComputedShare> ComputeShares(List<ParticipantShareInput> selected)
    {
        return Input.SplitMode switch
        {
            SplitMode.Equal => ComputeEqualShares(selected),
            SplitMode.CustomAmount => ComputeCustomAmountShares(selected),
            SplitMode.CustomRatio => ComputeRatioShares(selected),
            _ => ComputeEqualShares(selected)
        };
    }

    private List<ComputedShare> ComputeEqualShares(List<ParticipantShareInput> selected)
    {
        var totalAmount = decimal.Round(Input.Amount, 2, MidpointRounding.AwayFromZero);
        var totalCents = decimal.ToInt64(totalAmount * 100m);
        var baseCents = totalCents / selected.Count;
        var remainderCents = totalCents - (baseCents * selected.Count);

        var remainderTargetPersonId = selected.Any(participant => participant.PersonId == Input.PaidByPersonId)
            ? Input.PaidByPersonId
            : selected[0].PersonId;

        var result = new List<ComputedShare>();
        foreach (var participant in selected)
        {
            var cents = baseCents;
            if (participant.PersonId == remainderTargetPersonId)
            {
                cents += remainderCents;
            }

            var amount = cents / 100m;
            if (amount <= 0)
            {
                ModelState.AddModelError(string.Empty, "Amount is too small for an equal split with selected participants.");
                return new List<ComputedShare>();
            }

            result.Add(new ComputedShare(participant.PersonId, amount));
        }

        return result;
    }

    private List<ComputedShare> ComputeCustomAmountShares(List<ParticipantShareInput> selected)
    {
        var result = new List<ComputedShare>();
        foreach (var participant in selected)
        {
            var amount = decimal.Round(participant.CustomAmount ?? 0m, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0)
            {
                ModelState.AddModelError(string.Empty, $"Custom amount for {participant.PersonName} must be greater than zero.");
                return new List<ComputedShare>();
            }

            result.Add(new ComputedShare(participant.PersonId, amount));
        }

        var customTotal = result.Sum(item => item.Amount);
        if (decimal.Abs(customTotal - Input.Amount) > 0.01m)
        {
            ModelState.AddModelError(string.Empty, $"Custom amounts must total exactly {Input.Amount:0.00}.");
            return new List<ComputedShare>();
        }

        return result;
    }

    private List<ComputedShare> ComputeRatioShares(List<ParticipantShareInput> selected)
    {
        var weightedParticipants = new List<(int PersonId, string Name, decimal Ratio)>();
        foreach (var participant in selected)
        {
            var ratio = participant.Ratio ?? 0m;
            if (ratio <= 0)
            {
                ModelState.AddModelError(string.Empty, $"Ratio for {participant.PersonName} must be greater than zero.");
                return new List<ComputedShare>();
            }

            weightedParticipants.Add((participant.PersonId, participant.PersonName, ratio));
        }

        var totalRatio = weightedParticipants.Sum(item => item.Ratio);
        if (totalRatio <= 0)
        {
            ModelState.AddModelError(string.Empty, "Total ratio must be greater than zero.");
            return new List<ComputedShare>();
        }

        var result = new List<ComputedShare>();
        var runningTotal = 0m;
        for (var index = 0; index < weightedParticipants.Count; index++)
        {
            var participant = weightedParticipants[index];
            decimal amount;
            if (index == weightedParticipants.Count - 1)
            {
                amount = decimal.Round(Input.Amount - runningTotal, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                amount = decimal.Round(Input.Amount * participant.Ratio / totalRatio, 2, MidpointRounding.AwayFromZero);
                runningTotal += amount;
            }

            if (amount <= 0)
            {
                ModelState.AddModelError(string.Empty, "Amount is too small for ratio split with selected participants.");
                return new List<ComputedShare>();
            }

            result.Add(new ComputedShare(participant.PersonId, amount));
        }

        return result;
    }

    private void EnsureParticipantRows()
    {
        var currentRows = ParticipantShares
            .GroupBy(row => row.PersonId)
            .ToDictionary(group => group.Key, group => group.First());

        ParticipantShares = PersonOptions
            .Select(person =>
            {
                if (currentRows.TryGetValue(person.Id, out var row))
                {
                    row.PersonName = person.Name;
                    row.Ratio ??= 1m;
                    return row;
                }

                return new ParticipantShareInput
                {
                    PersonId = person.Id,
                    PersonName = person.Name,
                    Ratio = 1m
                };
            })
            .ToList();
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

    public sealed class ExpenseInputModel
    {
        [Required]
        [StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
        public decimal Amount { get; set; }

        public int PaidByPersonId { get; set; }

        public SplitMode SplitMode { get; set; } = SplitMode.Equal;

        [StringLength(80)]
        public string? Category { get; set; }
    }

    public sealed class ParticipantShareInput
    {
        public int PersonId { get; set; }
        public string PersonName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public decimal? CustomAmount { get; set; }
        public decimal? Ratio { get; set; } = 1m;
    }

    private sealed record ComputedShare(int PersonId, decimal Amount);
}
