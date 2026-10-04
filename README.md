[![NuGet](https://img.shields.io/nuget/v/Elmah.AzureTableStorage.svg)](https://www.nuget.org/packages/Elmah.AzureTableStorage)

Elmah.AzureTableStorage
======================

Elmah plugin for Azure Table Storage

Setup 
-----

If you use NuGet to add this project, option 1 will be used, by default.

### Option 1

By connection string name:

    <errorLog type="Elmah.AzureTableStorage.AzureTableStorageErrorLog, Elmah.AzureTableStorage"
        connectionStringName="ElmahAzureTableStorage" />
        
Make sure you add an element under the `connectionStrings` element.

### Option 2

By application setting:

    <errorLog type="Elmah.AzureTableStorage.AzureTableStorageErrorLog, Elmah.AzureTableStorage"
        connectionStringAppKey="ElmahAzureTableStorage" />
        
Make sure you add an element under the `appSettings` element.

### Option 3

By connection string:

    <errorLog type="Elmah.AzureTableStorage.AzureTableStorageErrorLog, Elmah.AzureTableStorage" 
        connectionString="UseDevelopmentStorage=true" />


Configuration Parameters
-----

### Application Name

Isolate per application in a single Elmah store

    <errorLog type="Elmah.AzureTableStorage.AzureTableStorageErrorLog, Elmah.AzureTableStorage" 
        connectionString="UseDevelopmentStorage=true" applicationName="MyApp" />

### Table Name

Set a custom table name (defaults to "Elmah"):

    <errorLog type="Elmah.AzureTableStorage.AzureTableStorageErrorLog, Elmah.AzureTableStorage" 
        connectionString="UseDevelopmentStorage=true" tableName="MyCustomElmahTable" />

Development
-----------

The library, demo, and tests target .NET Framework 4.8. Use a .NET 10 SDK
and Python 3 for the verification script. Windows runs the tests on .NET
Framework; Linux runs the same test assembly on Mono.

On this Ubuntu development VM, tools can be installed inside the repository
without changing the host OS:

    python3 eng/bootstrap-toolchain.py
    python3 eng/bootstrap-mono.py
    python3 eng/verify.py

On Windows with a .NET 10 SDK installed, run:

    python eng/verify.py

Verification restores the locked dependency graph, builds the entire solution
in Release with warnings treated as errors, runs all NUnit tests, enforces at
least 80% line and branch coverage across the complete library, and creates a
NuGet package. Reports and the package are written to the ignored artifacts
directory. Tests use an in-memory Azure SDK client and do not contact Azure.
Visual Studio Test Explorer and dotnet test are also supported on Windows.
The direct runner uses Coverlet's in-process tracker registry on Mono to flush
recorded hits before shutdown; this avoids a Mono ProcessExit formatting error.

Package versions are managed in Directory.Packages.props, including current
transitive dependency versions, and each project records its resolved graph
in packages.lock.json. To review available stable updates:

    dotnet list Elmah.AzureTableStorage.sln package --outdated --include-transitive

After updating packages, restore/build to refresh the lock files, then refresh
the demo's Web.config redirects from its generated configuration:

    python3 eng/sync-demo-redirects.py

The demo uses local development storage by default. On Windows, open the
solution in Visual Studio with the ASP.NET workload and use IIS Express.

Storage behavior
----------------

Writes complete before Log returns, and storage failures propagate to ELMAH.
Lists honor the requested page, and individual lookups use both the application
partition and the error identifier. Missing errors return null. Azure Table
Storage has no inexpensive total-count operation, so a non-final page reports
a lower bound that includes one lookahead record for the next-page link.

New identifiers contain reverse UTC ticks followed by a GUID to avoid collisions
while retaining newest-first ordering. Previously stored numeric identifiers
remain readable. Application keys keep their existing Base64 encoding, with
empty application names represented by an empty partition key.
