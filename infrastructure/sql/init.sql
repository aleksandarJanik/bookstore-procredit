IF DB_ID(N'BookstoreDb') IS NULL CREATE DATABASE BookstoreDb;
GO
IF DB_ID(N'KeycloakDb') IS NULL CREATE DATABASE KeycloakDb COLLATE Latin1_General_100_CI_AS_SC_UTF8;
GO

ALTER DATABASE KeycloakDb SET READ_COMMITTED_SNAPSHOT ON;
GO

USE master;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'bookstore_app')
    EXEC(N'CREATE LOGIN bookstore_app WITH PASSWORD = ''$(BookstorePassword)'', CHECK_POLICY = ON');
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'keycloak_app')
    EXEC(N'CREATE LOGIN keycloak_app WITH PASSWORD = ''$(KeycloakPassword)'', CHECK_POLICY = ON');
GO

USE BookstoreDb;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'bookstore_app')
    CREATE USER bookstore_app FOR LOGIN bookstore_app;
ALTER ROLE db_owner ADD MEMBER bookstore_app;
GO

USE KeycloakDb;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'keycloak_app')
    CREATE USER keycloak_app FOR LOGIN keycloak_app;
ALTER ROLE db_owner ADD MEMBER keycloak_app;
GO
