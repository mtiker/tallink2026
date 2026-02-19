using Microsoft.EntityFrameworkCore;
using TallinkFinance.Models;

namespace TallinkFinance.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseShare> ExpenseShares => Set<ExpenseShare>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Person>()
            .HasIndex(person => new { person.EventId, person.Name });

        modelBuilder.Entity<Expense>()
            .HasIndex(expense => new { expense.EventId, expense.Date });

        modelBuilder.Entity<ExpenseShare>()
            .HasIndex(share => new { share.ExpenseId, share.PersonId })
            .IsUnique();

        modelBuilder.Entity<Payment>()
            .HasIndex(payment => new { payment.EventId, payment.Date });

        modelBuilder.Entity<Expense>()
            .Property(expense => expense.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ExpenseShare>()
            .Property(share => share.ShareAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Payment>()
            .Property(payment => payment.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Person>()
            .HasOne(person => person.Event)
            .WithMany(@event => @event.People)
            .HasForeignKey(person => person.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Expense>()
            .HasOne(expense => expense.Event)
            .WithMany(@event => @event.Expenses)
            .HasForeignKey(expense => expense.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Expense>()
            .HasOne(expense => expense.PaidByPerson)
            .WithMany(person => person.PaidExpenses)
            .HasForeignKey(expense => expense.PaidByPersonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExpenseShare>()
            .HasOne(share => share.Expense)
            .WithMany(expense => expense.Shares)
            .HasForeignKey(share => share.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExpenseShare>()
            .HasOne(share => share.Person)
            .WithMany(person => person.ExpenseShares)
            .HasForeignKey(share => share.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne(payment => payment.Event)
            .WithMany(@event => @event.Payments)
            .HasForeignKey(payment => payment.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .HasOne(payment => payment.FromPerson)
            .WithMany(person => person.OutgoingPayments)
            .HasForeignKey(payment => payment.FromPersonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne(payment => payment.ToPerson)
            .WithMany(person => person.IncomingPayments)
            .HasForeignKey(payment => payment.ToPersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
