# Bookstore

A .NET 10 API and Angular 22 client backed by SQL Server and secured by Keycloak. The API provides OAuth-protected book CRUD plus title/author search with pagination.

## Run the complete solution

Requirements: Docker Desktop. From the repository root:

```powershell
docker compose up --build -d
```

Then open:

- Angular client: http://localhost:4201
- Swagger UI: http://localhost:5000/swagger
- Keycloak admin: http://localhost:8080/admin
- Readiness check: http://localhost:5000/health/ready

The first start can take about a minute while SQL Server and Keycloak initialize. Check progress with `docker compose ps` and `docker compose logs -f keycloak api`.

Local demo credentials:

- Librarian: `reader` / `Reader123!`
- Keycloak admin: `admin` / `Admin_Local123!`
- Service client: `bookstore-service` / `bookstore-service-local-secret`

Sign in to the Angular client with the librarian account. The **Librarian desk** lets you create a book, retrieve one by ID, and delete the retrieved book. When creating a book, select an existing author or enter a new author name; SQL Server assigns new author IDs automatically. The catalogue below it provides title/author search and pagination.

Use [Bookstore.Api.http](backend/Bookstore.Api/Bookstore.Api.http) in Visual Studio, VS Code REST Client, or JetBrains HTTP Client to obtain a service token and exercise CRUD.

For book writes, send `authorId: null` to create a new author automatically, or send an existing author ID and its matching name to reuse that author.

Stop the stack with `docker compose down`. Add `-v` only when you intentionally want to delete the local databases.

## Security model

- CRUD requires the `books.manage` scope and a token issued to the confidential `bookstore-service` client (client credentials flow).
- Search requires the `books.search` scope and a signed-in user.
- Browser-based create, retrieve, and delete requests use `/api/ui/books`. This authenticated back-end-for-frontend route requires the `bookstore-editor` realm role, then obtains the service token on the server. The confidential client secret is never sent to Angular.
- The browser uses Authorization Code with PKCE. This is the current secure replacement for the OAuth implicit flow: tokens are not returned in the redirect URL and are held only in memory.
- SQL Server contains separate `BookstoreDb` and `KeycloakDb` databases with separate application logins.

The checked-in passwords are local-development defaults. For deployment, copy `.env.example` to `.env`, replace every secret, use HTTPS, run Keycloak in production mode, restrict CORS/redirect URIs, persist encryption keys, and move migrations to a controlled deployment step.

## Develop and test

`global.json` pins the .NET SDK version. Open `Bookstore.sln` in Visual Studio, or run these command-line checks with .NET 10 and Node 24 installed:

```powershell
dotnet test Bookstore.sln
npm --prefix frontend/bookstore-ui ci
npm --prefix frontend/bookstore-ui test -- --run
npm --prefix frontend/bookstore-ui run build
```

The API tests use a temporary SQL Server Testcontainer, apply the real EF Core migration, and cover authorization, CRUD, the browser management route, search/pagination, validation, and author conflicts. Angular tests cover the management service calls and authenticated UI.

## Project layout

```text
backend/Bookstore.Api        REST API, EF Core migrations, Swagger
backend/Bookstore.Api.Tests  SQL Server integration tests
frontend/bookstore-ui        Angular application
infrastructure/keycloak      Imported local realm and OAuth clients
infrastructure/sql           Idempotent database/login initialization
compose.yaml                 Complete local environment
```
