$here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Invoke-Test([string]$name) {    
    $file = "$here\Samples\$name"
    Copy-Item "$file" "$file.tmp" -Force
	Update-OctopusApplicationConfigurationFile "$file.tmp" | Out-Host
    [xml]$out = Get-Content "$file.tmp"
    Remove-Item "$file.tmp"
    return $out
}

Describe "Tentacle.ConfigurationVariables" {
    It "App.config" {
        $OctopusParameters["WelcomeMessage"] = "I for one welcome our new PowerShell overlords" 
        $OctopusParameters["MyDb1"] = "Server=(local);Database=foo" 
	
		$x = Invoke-Test "App.config"

        (Select-XML $x -xpath "//*[local-name()='appSettings']/*[@key='WelcomeMessage']/@value") | Should Be "I for one welcome our new PowerShell overlords"
        (Select-XML $x -xpath "//*[local-name()='connectionStrings']/*[@name='MyDb1']/@connectionString") | Should Be "Server=(local);Database=foo"
    }
	
    It "NoHeader.config" {
        $OctopusParameters["WelcomeMessage"] = "I for one welcome our new PowerShell overlords" 
		        
		$x = Invoke-Test "NoHeader.config"

        (Select-XML $x -xpath "//*[local-name()='appSettings']/*[@key='WelcomeMessage']/@value") | Should Be "I for one welcome our new PowerShell overlords"
        $x.InnerXml.StartsWith("<configuration") | Should Be $true
	}

    It "CrazyNamespace.config" {
        $OctopusParameters["WelcomeMessage"] = "I for one welcome our new PowerShell overlords" 
        $OctopusParameters["MyDb1"] = "Server=(local);Database=foo" 
	
		$x = Invoke-Test "CrazyNamespace.config"

        (Select-XML $x -xpath "//*[local-name()='appSettings']/*[@key='WelcomeMessage']/@value") | Should Be "I for one welcome our new PowerShell overlords"
        (Select-XML $x -xpath "//*[local-name()='connectionStrings']/*[@name='MyDb1']/@connectionString") | Should Be "Server=(local);Database=foo"
    }
	
    It "StrongTyped.config" {
        $OctopusParameters["WelcomeMessage"] = "I for one welcome our new PowerShell overlords" 
		
		$x = Invoke-Test "StrongTyped.config"

        (Select-XML $x -xpath "//AppSettings.Properties.Settings/setting[@name='WelcomeMessage']/value") | Should Be "I for one welcome our new PowerShell overlords"
    }
}
