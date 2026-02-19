using System.ComponentModel.DataAnnotations;

namespace TallinkFinance.Models;

public class Expense
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int PaidByPersonId { get; set; }

    public Event? Event { get; set; }
    public Person? PaidByPerson { get; set; }

    [Required]
    [StringLength(250)]
    public string Title { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [StringLength(80)]
    public string? Category { get; set; }

    public ICollection<ExpenseShare> Shares { get; set; } = new List<ExpenseShare>();
}
