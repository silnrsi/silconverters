@echo off

REM Command line arguments and defined properties.
SET MsiFileName=%1
SHIFT

SET UseInsignia=%1
SHIFT

REM Path to WiX v4+ wix.exe (from the WixToolset.Sdk NuGet package), used to detach/reattach the bundle engine.
SET WixExe=%1
SHIFT

if [%UseInsignia%]==[] (
	echo signing %MsiFileName%
	call signingProxy %MsiFileName%
	exit /b
)

echo signing bundle exe %MsiFileName%
REM A Burn bundle can't be signed directly: the engine must be detached, signed, and reattached first, or the
REM attached container (MSI, vc_redist, etc.) can't be found at install time and Burn prompts the user to browse
REM for the bundle exe. So fail the build if any of the detach/reattach steps fail rather than falling through
REM to signing the bundle as-is.
if [%WixExe%]==[] (
	echo ERROR: no path to wix.exe was passed to buildBaseInstaller.bat; cannot sign bundle %MsiFileName%
	goto :fail
)
if exist engine.exe del /f engine.exe
%WixExe% burn detach %MsiFileName% -engine engine.exe
if errorlevel 1 (
	echo ERROR: failed to detach the bundle engine from %MsiFileName%
	goto :fail
)
call signingProxy engine.exe
%WixExe% burn reattach %MsiFileName% -engine engine.exe -o %MsiFileName%
if errorlevel 1 (
	echo ERROR: failed to reattach the signed engine to %MsiFileName%
	goto :fail
)
call signingProxy %MsiFileName%
exit /b

:fail
exit /b 1
