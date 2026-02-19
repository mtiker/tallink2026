using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;

namespace TallinkFinance.Services;

public sealed class BalanceCalculatorService
{
    private const decimal Epsilon = 0.0001m;
    private readonly AppDbContext _context;

    public BalanceCalculatorService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<DebtEntry>> GetDebtsAsync(int eventId, bool includePayments = true)
    {
        var raw = new Dictionary<(int DebtorId, int CreditorId), decimal>();

        var expenseRows = await (
                from share in _context.ExpenseShares.AsNoTracking()
                join expense in _context.Expenses.AsNoTracking()
                    on share.ExpenseId equals expense.Id
                where expense.EventId == eventId
                select new
                {
                    DebtorId = share.PersonId,
                    CreditorId = expense.PaidByPersonId,
                    Amount = share.ShareAmount
                })
            .ToListAsync();

        foreach (var row in expenseRows)
        {
            if (row.DebtorId == row.CreditorId)
            {
                continue;
            }

            AddDelta(raw, (row.DebtorId, row.CreditorId), row.Amount);
        }

        if (includePayments)
        {
            var payments = await _context.Payments
                .AsNoTracking()
                .Where(payment => payment.EventId == eventId)
                .Select(payment => new
                {
                    payment.FromPersonId,
                    payment.ToPersonId,
                    payment.Amount
                })
                .ToListAsync();

            foreach (var payment in payments)
            {
                AddDelta(raw, (payment.FromPersonId, payment.ToPersonId), -payment.Amount);
            }
        }

        return Normalize(raw);
    }

    public async Task<List<PersonBalance>> GetBalancesAsync(int eventId, bool includePayments = true)
    {
        var personIds = await _context.People
            .AsNoTracking()
            .Where(person => person.EventId == eventId)
            .Select(person => person.Id)
            .ToListAsync();

        var debts = await GetDebtsAsync(eventId, includePayments);
        var owesByPersonId = personIds.ToDictionary(personId => personId, _ => 0m);
        var owedByPersonId = personIds.ToDictionary(personId => personId, _ => 0m);

        foreach (var debt in debts)
        {
            owesByPersonId[debt.DebtorId] += debt.Amount;
            owedByPersonId[debt.CreditorId] += debt.Amount;
        }

        return personIds
            .Select(personId => new PersonBalance(
                personId,
                decimal.Round(owesByPersonId[personId], 2),
                decimal.Round(owedByPersonId[personId], 2)))
            .OrderByDescending(balance => balance.Net)
            .ThenBy(balance => balance.PersonId)
            .ToList();
    }

    private static List<DebtEntry> Normalize(Dictionary<(int DebtorId, int CreditorId), decimal> raw)
    {
        var normalizedDirection = new Dictionary<(int DebtorId, int CreditorId), decimal>();
        foreach (var ((debtorId, creditorId), amountRaw) in raw)
        {
            if (debtorId == creditorId || decimal.Abs(amountRaw) < Epsilon)
            {
                continue;
            }

            var amount = amountRaw;
            var debtor = debtorId;
            var creditor = creditorId;

            if (amount < 0)
            {
                (debtor, creditor) = (creditor, debtor);
                amount = -amount;
            }

            AddDelta(normalizedDirection, (debtor, creditor), amount);
        }

        var netted = new Dictionary<(int DebtorId, int CreditorId), decimal>();
        foreach (var ((debtorId, creditorId), amountRaw) in normalizedDirection)
        {
            if (debtorId == creditorId || decimal.Abs(amountRaw) < Epsilon)
            {
                continue;
            }

            var amount = amountRaw;
            if (netted.TryGetValue((creditorId, debtorId), out var oppositeAmount))
            {
                if (oppositeAmount > amount + Epsilon)
                {
                    netted[(creditorId, debtorId)] = oppositeAmount - amount;
                    continue;
                }

                if (amount > oppositeAmount + Epsilon)
                {
                    netted.Remove((creditorId, debtorId));
                    AddDelta(netted, (debtorId, creditorId), amount - oppositeAmount);
                    continue;
                }

                netted.Remove((creditorId, debtorId));
                continue;
            }

            AddDelta(netted, (debtorId, creditorId), amount);
        }

        return netted
            .Where(item => item.Value > Epsilon)
            .Select(item => new DebtEntry(
                item.Key.DebtorId,
                item.Key.CreditorId,
                decimal.Round(item.Value, 2)))
            .OrderBy(item => item.DebtorId)
            .ThenBy(item => item.CreditorId)
            .ToList();
    }

    private static void AddDelta(
        IDictionary<(int DebtorId, int CreditorId), decimal> dictionary,
        (int DebtorId, int CreditorId) key,
        decimal delta)
    {
        if (key.DebtorId == key.CreditorId || decimal.Abs(delta) < Epsilon)
        {
            return;
        }

        if (dictionary.TryGetValue(key, out var current))
        {
            dictionary[key] = current + delta;
            return;
        }

        dictionary[key] = delta;
    }
}

public sealed record DebtEntry(int DebtorId, int CreditorId, decimal Amount);

public sealed record PersonBalance(int PersonId, decimal OwesTotal, decimal IsOwedTotal)
{
    public decimal Net => IsOwedTotal - OwesTotal;
}
