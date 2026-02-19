using System.ComponentModel.DataAnnotations;

namespace TallinkFinance.Models;

public class Event
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "EUR";

    public ICollection<Person> People { get; set; } = new List<Person>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
