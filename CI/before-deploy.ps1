# This script is run by AppVeyor's deploy agent before the deploy

$bkupfolder = "$env:application_path\..\bkup"
$webroot = "$env:application_path"

Write-Output "Running pre-deploy script"
Write-Output "--------------------------------------------------"
Write-Output "Backup folder: $bkupfolder"
Write-Output "Web root folder: $webroot"
Write-Output "Running script as: $env:userdomain\$env:username"

# stop execution of the deploy if the moves fail
$ErrorActionPreference = "Stop"

# backup web.config file
Write-Host "Moving web.config to bkup dir"
Copy-Item "$webroot\web.config" "$bkupfolder" -force

# backup connection string file
Write-Host "Moving web.connectionstrings.config to bkup dir"
Copy-Item "$webroot\web.connectionstrings.config" "$bkupfolder" -force

# backup theme overrides for all themes
Write-Host "Moving Theme *-overrides.less to bkup dir"
xcopy /S /D /I /Y "$webroot\Themes\*-overrides.less" "$bkupfolder\Themes"

# load the app offline template
If (Test-Path "$webroot\app_offline-template.htm"){
	Write-Host "Loading the app offline template"
	Copy-Item "$webroot\app_offline-template.htm" "$webroot\app_offline.htm" -force
}

Write-Output "Pre-deploy script complete"