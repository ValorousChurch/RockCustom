# This script is run by AppVeyor's deploy agent after the deploy

$bkupfolder = "$env:application_path\..\bkup"
$webroot = "$env:application_path"

Write-Output "Running post-deploy script"
Write-Output "--------------------------------------------------"
Write-Output "Backup folder: $bkupfolder"
Write-Output "Web root folder: $webroot"
Write-Output "Running script as: $env:userdomain\$env:username"

# move files back from bkup (IIS will auto-restart because we are moving web.config)
If (Test-Path "$bkupfolder"){
	Write-Host "Moving files back from bkup directory"
	Move-Item -Path "$bkupfolder\*" -Destination "$webroot" -Force
}

# remove the app offline flag
If (Test-Path "$webroot\app_offline.htm"){
	Write-Host "Removing app offline template"
	Remove-Item "$webroot\app_offline.htm"
}

# delete deploy scripts
If (Test-Path "$webroot\deploy.ps1"){
	Remove-Item "$webroot\deploy.ps1"
}
If (Test-Path "$webroot\before-deploy.ps1"){
	Remove-Item "$webroot\before-deploy.ps1"
}