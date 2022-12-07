@echo off

REM Command line arguments and defined properties.
SET MsiFileName=%1
SHIFT

SET UseInsignia=%1
SHIFT

if [%UseInsignia%]==[] (
	echo signing %MsiFileName%
	call signingProxy %MsiFileName%
) else (
	echo signing bundle exe %MsiFileName%
	@REM Sign the standard installer.
	insignia -ib %MsiFileName% -o engine.exe
	call signingProxy engine.exe
	insignia -ab engine.exe %MsiFileName% -o %MsiFileName%
	call signingProxy %MsiFileName%
)
