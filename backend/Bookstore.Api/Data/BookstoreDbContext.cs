using Bookstore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Data;

public sealed class BookstoreDbContext(DbContextOptions<BookstoreDbContext> options) : DbContext(options)
{
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Latin1_General_100_CI_AS_SC");

        modelBuilder.Entity<Author>(author =>
        {
            author.HasKey(x => x.AuthorId);
            author.Property(x => x.AuthorId).UseIdentityColumn();
            author.Property(x => x.Name).HasMaxLength(100).IsRequired();
            author.ToTable(table => table.HasCheckConstraint("CK_Authors_Name_Length", "LEN([Name]) BETWEEN 3 AND 100"));
        });

        modelBuilder.Entity<Book>(book =>
        {
            book.HasKey(x => x.BookId);
            book.Property(x => x.BookId).UseIdentityColumn();
            book.Property(x => x.Title).HasMaxLength(100).IsRequired();
            book.ToTable(table => table.HasCheckConstraint("CK_Books_Title_Length", "LEN([Title]) BETWEEN 3 AND 100"));
            book.HasIndex(x => x.Title);
            book.HasOne(x => x.Author)
                .WithMany(x => x.Books)
                .HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
