using System.ComponentModel.DataAnnotations;

namespace TallinkFinance.Models;

public class ExpenseShare
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public int PersonId { get; set; }

    public Expense? Expense { get; set; }
    public Person? Person { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal ShareAmount { get; set; }
}
