using System.ComponentModel.DataAnnotations;

namespace TallinkFinance.Models;

public class Person
{
    public int Id { get; set; }
    public int EventId { get; set; }

    public Event? Event { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Contact { get; set; }

    public ICollection<Expense> PaidExpenses { get; set; } = new List<Expense>();
    public ICollection<ExpenseShare> ExpenseShares { get; set; } = new List<ExpenseShare>();
    public ICollection<Payment> OutgoingPayments { get; set; } = new List<Payment>();
    public ICollection<Payment> IncomingPayments { get; set; } = new List<Payment>();
}
