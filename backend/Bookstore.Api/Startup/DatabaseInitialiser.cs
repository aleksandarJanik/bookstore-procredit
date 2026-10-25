using Bookstore.Api.Data;
using Bookstore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Startup;

public static class DatabaseInitialiser
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:ApplyMigrations", false))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookstoreDbContext>();
        await db.Database.MigrateAsync();

        if (app.Configuration.GetValue("Database:SeedData", false) && !await db.Books.AnyAsync())
        {
            var orwell = new Author { Name = "George Orwell" };
            var austen = new Author { Name = "Jane Austen" };
            db.AddRange(
                new Book { Author = orwell, Title = "Nineteen Eighty-Four", SubTitle = "A Novel" },
                new Book { Author = orwell, Title = "Animal Farm", SubTitle = "A Fairy Story" },
                new Book { Author = austen, Title = "Pride and Prejudice" });
            await db.SaveChangesAsync();
        }
    }
}
