#!/usr/bin/env bash
set -euo pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd

until "$SQLCMD" -S sqlserver -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; do
  sleep 2
done

"$SQLCMD" -S sqlserver -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -v BookstorePassword="$BOOKSTORE_DB_PASSWORD" KeycloakPassword="$KEYCLOAK_DB_PASSWORD" \
  -i /scripts/init.sql
