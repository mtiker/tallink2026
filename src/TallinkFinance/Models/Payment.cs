using System.ComponentModel.DataAnnotations;

namespace TallinkFinance.Models;

public class Payment
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int FromPersonId { get; set; }
    public int ToPersonId { get; set; }

    public Event? Event { get; set; }
    public Person? FromPerson { get; set; }
    public Person? ToPerson { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [StringLength(300)]
    public string? Note { get; set; }
}
