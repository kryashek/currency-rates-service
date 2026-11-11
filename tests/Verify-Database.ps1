#requires -Version 7.2
[CmdletBinding()]
param([string] $PostgresBin)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($PostgresBin)) {
    $PostgresBin = Join-Path $repo '.tools/postgresql16/pgsql/bin'
}
$PostgresBin = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($PostgresBin)
foreach ($executable in @('postgres.exe', 'psql.exe', 'pg_ctl.exe', 'initdb.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PostgresBin $executable) -PathType Leaf)) {
        throw "PostgreSQL 16 executable '$executable' was not found in '$PostgresBin'. Supply -PostgresBin with the full path to the PostgreSQL 16 bin directory."
    }
}
$dotnet = Join-Path $repo '.tools/dotnet8/dotnet.exe'
$migrator = Join-Path $repo 'src/Tools/CurrencyRates.DbMigrator/bin/Release/net8.0/CurrencyRates.DbMigrator.dll'
$psql = Join-Path $PostgresBin 'psql.exe'
$pgctl = Join-Path $PostgresBin 'pg_ctl.exe'
$initdb = Join-Path $PostgresBin 'initdb.exe'
$version = & (Join-Path $PostgresBin 'postgres.exe') --version
if ($LASTEXITCODE -ne 0 -or $version -notmatch '\b16\.') { throw 'PostgreSQL 16 binaries are required.' }
$run = Join-Path $repo ('.tools/checks/database/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$data = Join-Path $run 'data'
$envNames = @('PGPASSWORD','PGAPPNAME','CRS_IDENTITY_PASSWORD','CRS_FINANCE_PASSWORD','CRS_MIGRATOR_PASSWORD',
    'ConnectionStrings__Migrator','CRS_TEST_IDENTITY','CRS_TEST_FINANCE','DOTNET_ROOT','DOTNET_CLI_HOME','NUGET_PACKAGES')
$saved = @{}
foreach ($name in $envNames) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
function New-Password { [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
$adminPassword = New-Password
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
$started = $false
$children = [Collections.Generic.List[Diagnostics.Process]]::new()

function Sql([string] $Database, [string] $Statement) {
    $result = & $psql -X -w -q -t -A -h 127.0.0.1 -p $port -U postgres -d $Database -v ON_ERROR_STOP=1 -c $Statement 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Verification SQL failed in $Database (diagnostics suppressed)." }
    return ($result -join "`n").Trim()
}
function Assert-Equal($Actual, $Expected, [string] $Scenario) {
    if ([string]$Actual -cne [string]$Expected) { throw "$Scenario failed: expected $Expected, got $Actual" }
}
function Connect-String([string] $Database, [string] $User, [string] $Password) {
    return "Host=127.0.0.1;Port=$port;Database=$Database;Username=$User;Password=$Password;Timeout=5;Command Timeout=15;Include Error Detail=false"
}
function Bootstrap([string] $Database) {
    & (Join-Path $repo 'database/Bootstrap.ps1') -PsqlPath $psql -DatabaseHost 127.0.0.1 -Port $port -DatabaseName $Database
}
function Start-Migrator([string] $Database) {
    $info = [Diagnostics.ProcessStartInfo]::new($dotnet)
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.ArgumentList.Add($migrator)
    $info.Environment['ConnectionStrings__Migrator'] = Connect-String $Database 'rates_migrator' $env:CRS_MIGRATOR_PASSWORD
    $process = [Diagnostics.Process]::Start($info)
    $children.Add($process)
    return $process
}
function Finish-Migrator([Diagnostics.Process] $Process, [int] $Expected) {
    if (-not $Process.WaitForExit(30000)) { $Process.Kill($true); throw 'Migrator did not exit within 30 seconds.' }
    $output = $Process.StandardOutput.ReadToEnd() + $Process.StandardError.ReadToEnd()
    if ($Process.ExitCode -ne $Expected) { Write-Output $output.Trim() }
    Assert-Equal $Process.ExitCode $Expected 'Migrator exit code'
    foreach ($name in @('CRS_IDENTITY_PASSWORD','CRS_FINANCE_PASSWORD','CRS_MIGRATOR_PASSWORD')) {
        if ($output.Contains([Environment]::GetEnvironmentVariable($name))) { throw 'Secret found in migrator output.' }
    }
    Write-Output $output.Trim()
}
function Migrate([string] $Database, [int] $Expected = 0) { Finish-Migrator (Start-Migrator $Database) $Expected }
function Wait-Sql([string] $Database, [string] $Statement) {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        if ((Sql $Database $Statement) -eq 't') { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Timed out waiting for the database synchronization point.'
}
function Start-Blocker([string] $Database) {
    $null = Sql $Database 'CREATE TABLE identity."__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL); ALTER TABLE identity."__EFMigrationsHistory" OWNER TO rates_migrator'
    $info = [Diagnostics.ProcessStartInfo]::new($psql)
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true; $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($arg in @('-X','-w','-q','-h','127.0.0.1','-p',"$port",'-U','postgres','-d',$Database,'-v','ON_ERROR_STOP=1')) { $info.ArgumentList.Add($arg) }
    $info.Environment['PGAPPNAME'] = 'crs-verification-blocker'
    $process = [Diagnostics.Process]::Start($info)
    $children.Add($process)
    $process.StandardInput.WriteLine('BEGIN; LOCK TABLE identity."__EFMigrationsHistory" IN ACCESS EXCLUSIVE MODE; SELECT pg_sleep(120); COMMIT;')
    $process.StandardInput.Close()
    Wait-Sql $Database "SELECT EXISTS (SELECT 1 FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid WHERE a.datname=current_database() AND a.application_name='crs-verification-blocker' AND l.mode='AccessExclusiveLock' AND l.granted)"
    return $process
}
function Stop-Blocker([string] $Database) {
    $null = Sql $Database "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname=current_database() AND application_name='crs-verification-blocker'"
}
try {
    $env:DOTNET_ROOT = Join-Path $repo '.tools/dotnet8'
    $env:DOTNET_CLI_HOME = Join-Path $repo '.tools/cli-home'
    $env:NUGET_PACKAGES = Join-Path $repo '.tools/nuget/packages'
    $env:PGPASSWORD = $adminPassword
    $env:CRS_IDENTITY_PASSWORD = New-Password
    $env:CRS_FINANCE_PASSWORD = New-Password
    $env:CRS_MIGRATOR_PASSWORD = New-Password
    $pwFile = Join-Path $run 'initdb-password'
    [IO.File]::WriteAllText($pwFile, $adminPassword, [Text.UTF8Encoding]::new($false))
    & $initdb -D $data -U postgres --encoding=UTF8 --no-locale --auth=scram-sha-256 "--pwfile=$pwFile" *> (Join-Path $run 'initdb.log')
    if ($LASTEXITCODE -ne 0) { throw 'Isolated initdb failed.' }
    Remove-Item -LiteralPath $pwFile
    & $pgctl -D $data -l (Join-Path $run 'postgres.log') -o "-h 127.0.0.1 -p $port" -w start
    if ($LASTEXITCODE -ne 0) { throw 'Isolated PostgreSQL startup failed.' }
    $started = $true
    Write-Output "Isolated $version on loopback port $port"

    $clean = 'crs_verify_clean'
    Bootstrap $clean
    # A rerun must preserve existing passwords, even when new values are supplied.
    $original = $env:CRS_MIGRATOR_PASSWORD
    $env:CRS_MIGRATOR_PASSWORD = New-Password
    Bootstrap $clean
    $env:CRS_MIGRATOR_PASSWORD = $original
    Migrate $clean
    Assert-Equal (Sql $clean 'SELECT count(*) FROM identity."__EFMigrationsHistory"') 1 'Identity history'
    Assert-Equal (Sql $clean 'SELECT count(*) FROM finance."__EFMigrationsHistory"') 1 'Finance history'
    Assert-Equal (Sql $clean 'SELECT count(*) FROM identity."user"') 0 'No seeded users'
    Assert-Equal (Sql $clean 'SELECT count(*) FROM finance.currency') 0 'No seeded currencies'
    Migrate $clean
    Assert-Equal (Sql $clean 'SELECT count(*) FROM finance."__EFMigrationsHistory"') 1 'Repeat history'
    Bootstrap $clean
    Write-Output 'PASS: clean database, bootstrap repeat/password preservation, migrator repeat.'

    $partial = 'crs_verify_partial'
    Bootstrap $partial
    $null = Sql $partial 'CREATE TABLE finance.import_state (sentinel int)'
    Migrate $partial 1
    Assert-Equal (Sql $partial 'SELECT count(*) FROM identity."__EFMigrationsHistory"') 1 'Identity retained after Finance failure'
    Assert-Equal (Sql $partial "SELECT to_regclass('finance.currency') IS NULL AND to_regclass('finance.import_batch') IS NULL") 't' 'Finance transaction rollback'
    Assert-Equal (Sql $partial 'SELECT count(*) FROM finance."__EFMigrationsHistory"') 0 'Failed migration not recorded'
    $null = Sql $partial 'DROP TABLE finance.import_state'
    Migrate $partial
    Migrate $partial
    Write-Output 'PASS: partial failure, transaction rollback, recovery and repeat.'

    $parallel = 'crs_verify_parallel'
    Bootstrap $parallel
    $blocker = Start-Blocker $parallel
    $first = Start-Migrator $parallel
    Wait-Sql $parallel "SELECT EXISTS (SELECT 1 FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid WHERE a.datname=current_database() AND a.application_name='CurrencyRates.DbMigrator' AND l.locktype='advisory' AND l.granted)"
    Migrate $parallel 3
    Stop-Blocker $parallel
    Finish-Migrator $first 0
    Migrate $parallel
    Write-Output 'PASS: real concurrent migrators; second exits 3, first completes.'

    $lost = 'crs_verify_connection_loss'
    Bootstrap $lost
    $blocker = Start-Blocker $lost
    $first = Start-Migrator $lost
    Wait-Sql $lost "SELECT EXISTS (SELECT 1 FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid WHERE a.datname=current_database() AND a.application_name='CurrencyRates.DbMigrator' AND l.locktype='advisory' AND l.granted)"
    $null = Sql $lost "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname=current_database() AND application_name='CurrencyRates.DbMigrator'"
    Finish-Migrator $first 1
    Stop-Blocker $lost
    Assert-Equal (Sql $lost "SELECT to_regclass('finance.currency') IS NULL") 't' 'Stop after lock connection loss'
    Migrate $lost
    Write-Output 'PASS: connection loss stops migrator and releases lock; retry succeeds.'

    $env:ConnectionStrings__Migrator = ''
    $invalid = & $dotnet $migrator 2>&1
    Assert-Equal $LASTEXITCODE 2 'Missing configuration'
    $marker = New-Password
    $env:ConnectionStrings__Migrator = "Password=$marker;NotAConnectionSetting=1"
    $invalid = (& $dotnet $migrator 2>&1) -join "`n"
    Assert-Equal $LASTEXITCODE 2 'Invalid configuration'
    if ($invalid.Contains($marker)) { throw 'Configuration secret was logged.' }
    $env:ConnectionStrings__Migrator = "Host=127.0.0.1;Port=1;Database=unreachable;Username=none;Timeout=1"
    $unavailable = & $dotnet $migrator 2>&1
    Assert-Equal $LASTEXITCODE 1 'Database unavailable'
    Write-Output 'PASS: failure exit codes and secret redaction.'

    $env:CRS_TEST_IDENTITY = Connect-String $clean 'identity_runtime' $env:CRS_IDENTITY_PASSWORD
    $env:CRS_TEST_FINANCE = Connect-String $clean 'finance_runtime' $env:CRS_FINANCE_PASSWORD
    & $dotnet test (Join-Path $repo 'CurrencyRates.sln') -c Release --no-build --no-restore --logger trx --results-directory $run
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL persistence or existing contract tests failed.' }
    Write-Output "PASS: runtime constraints/rights and EF round trips. Reports: $run"
}
finally {
    foreach ($child in $children) { if (-not $child.HasExited) { $child.Kill($true); $child.WaitForExit() }; $child.Dispose() }
    if ($started) { & $pgctl -D $data -m immediate -w stop | Out-Null }
    foreach ($name in $envNames) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
}
