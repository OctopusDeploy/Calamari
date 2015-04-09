## --------------------------------------------------------------------------------------
## Configuration
## --------------------------------------------------------------------------------------

$isEnabled = $OctopusParameters["Octopus.Action.RedGateDatabase.Enabled"]
if (!$isEnabled -or ![Bool]::Parse($isEnabled))
{
   exit 0
}

$connectionStrings = $OctopusParameters["Octopus.Action.RedGateDatabase.ConnectionStrings"]
$databaseName = $OctopusParameters["Octopus.Action.RedGateDatabase.DatabaseName"]
$compareOptions = $OctopusParameters["Octopus.Action.RedGateDatabase.CompareOptions"]
$postDeployValidation = $OctopusParameters["Octopus.Action.RedGateDatabase.PostDeployValidationEnabled"]
$SQL_CI_PATH = $OctopusParameters["Octopus.Action.RedGateDatabase.SqlCiPath"]
$SQL_COMPARE_PATH = $OctopusParameters["Octopus.Action.RedGateDatabase.SqlComparePath"]
$packageFilePath = $OctopusParameters["Octopus.Tentacle.CurrentDeployment.PackageFilePath"]
$packageExtractPath = $OctopusParameters["Octopus.Action.Package.CustomInstallationDirectory"]

$connectionArray = $connectionStrings.Split("`n")

# [String]::IsNullOrWhitespace is not available for .NET framework 3.5 and below
# so we need to write our own implementation.
function StringIsNullOrWhitespace([string] $string)
{
    if ($string -ne $null) { $string = $string.Trim() }
    return [string]::IsNullOrEmpty($string)
}


function RemoveEmptyOptions
{
    param($compareOptions)

    $options = @($compareOptions -split "[,;]" | % {$_.Trim()} | ? {! (StringIsNullOrWhitespace($_)) })
    $options = $options -join ','
    return $options
}

foreach ($connectionString in $connectionArray) 
{
    if (StringIsNullOrWhitespace($connectionString))
    {
        continue;
    }

    Write-Host "Parsing connection string: $connectionString"
    $conn = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $connectionString
    
    if (StringIsNullOrWhitespace($conn["Initial Catalog"]))
    {
        $conn["Initial Catalog"] = $databaseName;
    }

    # Builds SQL CI arguments from Octopus variables
    $database_server = $conn.DataSource
    $database_name = $conn["Initial Catalog"]
    $database_username = $conn.UserId
    $database_password = $conn.Password
    $arguments = "sync", "/databaseServer:$DATABASE_SERVER", "/databaseName:$DATABASE_NAME", "/package:$packageFilePath"

    # Specifies additional SQL Compare options
    $compareOptions = RemoveEmptyOptions($compareOptions)
    if ($compareOptions)
    {
        $arguments += "/additionalCompareArgs=`"/options:$compareOptions`""
    }
    
    if ($conn.IntegratedSecurity -eq $true) 
    {
        if ($database_username -or $database_password) {
            Write-Warning "Username and/or password specified when using Windows Authentication. The username and/or password will not be used."
        }

        Write-Output "Deploying to $database_server/$database_name using integrated Windows authentication as $env:USERDOMAIN\$env:USERNAME"
    }
    else 
    {
        if (!$database_username -or !$database_password)
        {
            throw "Username and/or password not specified. Please specify them or alternatively use Windows Authentication."
        }

        $arguments += "/databaseUserName:$database_username",
                      "/databasePassword:$database_password"

        Write-Output "Deploying to $database_server/$database_name using SQL Server username $database_username"
    }

    # Calls sqlCI.exe with the arguments in the $arguments array
    & $SQL_CI_PATH $arguments

    if ([System.Boolean]::Parse($postDeployValidation) -eq $true) 
    {
        Write-Output "Doing post deploy validation to check that the database has been updated to the correct state..."
        $sqlCompareArguments = "/Assertidentical",
                               "/scripts1:$packageExtractPath/db/state",
                               "/database2:$DATABASE_NAME",
                               "/server2:$DATABASE_SERVER"

        if ($conn.IntegratedSecurity -eq $false) {
            $sqlCompareArguments += "/UserName2:$DATABASE_USERNAME",
                                    "/Password2:$DATABASE_PASSWORD"
        }
        
        if ($compareOptions) 
        {
            $sqlCompareArguments += "/options:`"default,UseMigrationsV2,ignoretSQLt,$compareOptions`""
        }
        else
        {
            # The default comparison options that SQLCI uses to sync the NuGet package to the database.
            # We should use these defaults if no options were provided.
            $sqlCompareArguments += "/options:`"default,UseMigrationsV2,ignoretSQLt`""
        }
        
        Write-Output "Starting SQLCompare.exe with these arguments: $sqlCompareArguments"
        & $SQL_COMPARE_PATH $sqlCompareArguments
        if (!$?) {
            # Check if last exit code was zero. If it isn't we should throw
            throw "Post deploy validation failed."
        }
    }
    else
    {
        Write-Output "Skipping post deploy validation. To turn this on, please tick Post-deploy validation checkbox in this step."
    }
}
