Dim installer, database, view, result
Dim strPathMsi 

If WScript.Arguments.Count <> 1 Then
    WScript.Echo "Usage: cscript AddRebootPrompt.vbs <path to MSI>"
    WScript.Quit -1
End If

strPathMsi = WScript.Arguments(0)

Set installer = CreateObject("WindowsInstaller.Installer")
Set database = installer.OpenDatabase (strPathMsi, 1)
Set view = database.OpenView ("INSERT INTO Property (Property, Value) VALUES ('REBOOT', 'Force')")

WScript.Echo "Adding forced reboot prompt to install sequence."

view.Execute
database.Commit
WScript.Quit 0