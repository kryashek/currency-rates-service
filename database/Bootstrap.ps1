[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $PsqlPath,
    [string] $DatabaseName = 'currency_rates',
    [string] $DatabaseHost = 'localhost',
    [ValidateRange(1,65535)][int] $Port = 5432,
    [string] $AdminUser = 'postgres'
)
$ErrorActionPreference = 'Stop'
if ($DatabaseName -notmatch '^[a-z][a-z0-9_]{0,62}$' -or $DatabaseName -in @('postgres','template0','template1')) {
    throw 'Choose a dedicated lowercase database name (not a maintenance database).'
}
foreach ($name in @('CRS_IDENTITY_PASSWORD','CRS_FINANCE_PASSWORD','CRS_MIGRATOR_PASSWORD')) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
        throw "Set $name through the deployment secret store."
    }
}
# libpq reads the administrator password from PGPASSWORD/PGPASSFILE.
# Capture every diagnostic: PostgreSQL errors can contain SQL with secret literals.
$diagnostics = & $PsqlPath -X -w -q -h $DatabaseHost -p $Port -U $AdminUser -d postgres `
    -v ON_ERROR_STOP=1 -v "db_name=$DatabaseName" -f (Join-Path $PSScriptRoot 'bootstrap.sql') 2>&1
if ($LASTEXITCODE -ne 0) {
    throw 'Database bootstrap failed. Verify the administrative connection, dedicated database ownership and managed roles. Diagnostics were suppressed to protect secrets.'
}
Write-Output 'Database bootstrap completed. Existing role passwords were preserved.'
