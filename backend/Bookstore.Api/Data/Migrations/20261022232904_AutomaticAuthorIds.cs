using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookstore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AutomaticAuthorIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE [Books] DROP CONSTRAINT [FK_Books_Authors_AuthorId];

                CREATE TABLE [Authors_New] (
                    [AuthorId] int IDENTITY(1,1) NOT NULL,
                    [Name] nvarchar(100) NOT NULL,
                    CONSTRAINT [PK_Authors_New] PRIMARY KEY ([AuthorId]),
                    CONSTRAINT [CK_Authors_New_Name_Length] CHECK (LEN([Name]) BETWEEN 3 AND 100)
                );

                SET IDENTITY_INSERT [Authors_New] ON;
                INSERT INTO [Authors_New] ([AuthorId], [Name])
                SELECT [AuthorId], [Name] FROM [Authors];
                SET IDENTITY_INSERT [Authors_New] OFF;

                DROP TABLE [Authors];
                EXEC sp_rename N'[Authors_New]', N'Authors';
                EXEC sp_rename N'[dbo].[PK_Authors_New]', N'PK_Authors', N'OBJECT';
                EXEC sp_rename N'[dbo].[CK_Authors_New_Name_Length]', N'CK_Authors_Name_Length', N'OBJECT';
                DBCC CHECKIDENT (N'[Authors]', RESEED);

                ALTER TABLE [Books] ADD CONSTRAINT [FK_Books_Authors_AuthorId]
                    FOREIGN KEY ([AuthorId]) REFERENCES [Authors] ([AuthorId]) ON DELETE NO ACTION;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE [Books] DROP CONSTRAINT [FK_Books_Authors_AuthorId];

                CREATE TABLE [Authors_New] (
                    [AuthorId] int NOT NULL,
                    [Name] nvarchar(100) NOT NULL,
                    CONSTRAINT [PK_Authors_New] PRIMARY KEY ([AuthorId]),
                    CONSTRAINT [CK_Authors_New_Name_Length] CHECK (LEN([Name]) BETWEEN 3 AND 100)
                );

                INSERT INTO [Authors_New] ([AuthorId], [Name])
                SELECT [AuthorId], [Name] FROM [Authors];

                DROP TABLE [Authors];
                EXEC sp_rename N'[Authors_New]', N'Authors';
                EXEC sp_rename N'[dbo].[PK_Authors_New]', N'PK_Authors', N'OBJECT';
                EXEC sp_rename N'[dbo].[CK_Authors_New_Name_Length]', N'CK_Authors_Name_Length', N'OBJECT';

                ALTER TABLE [Books] ADD CONSTRAINT [FK_Books_Authors_AuthorId]
                    FOREIGN KEY ([AuthorId]) REFERENCES [Authors] ([AuthorId]) ON DELETE NO ACTION;
                """);
        }
    }
}
