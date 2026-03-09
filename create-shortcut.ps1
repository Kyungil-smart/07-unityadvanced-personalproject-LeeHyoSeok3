# 데스크탑에 Claude Code 바로가기 생성
$WshShell = New-Object -comObject WScript.Shell
$Desktop = [System.Environment]::GetFolderPath('Desktop')
$Shortcut = $WshShell.CreateShortcut("$Desktop\Claude - LeeHyoSeok3.lnk")
$Shortcut.TargetPath = "wt"
$Shortcut.Arguments = '-d "C:\Github\07-unityadvanced-personalproject-LeeHyoSeok3" cmd /k claude'
$Shortcut.WorkingDirectory = "C:\Github\07-unityadvanced-personalproject-LeeHyoSeok3"
$Shortcut.Description = "Claude Code - LeeHyoSeok3 프로젝트"
$Shortcut.Save()
Write-Host "바로가기가 데스크탑에 생성되었습니다: Claude - LeeHyoSeok3.lnk"