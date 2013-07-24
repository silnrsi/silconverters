' See the "EC Installation Readme.txt" file for details of these methods, and parameters
' This script is to add the converter for the SIL Heb Trans <> UNICODE Encoding Conversion
' and the fonts
mapName = "SIL Heb Trans<>UNICODE"
fileName = "MapsTables\SILHebTrans.tec"
encodingNameLhs = "SIL Heb Trans-1997"
encodingNameRhs = "UNICODE"

fontNameSILHebTrans = "SIL Heb Trans"
fontNameCharisSIL = "Charis SIL"

' vbscript doesn't allow import of tlb info, so redefine them here for documentation purposes
NormalizeFlags_None = 0
DontCare = &H0
Legacy_to_from_Unicode = 1
UnicodeEncodingConversion = &H1

' get the repository object and use it to add this converter
Dim aECs
Set aECs = CreateObject("SilEncConverters40.EncConverters")

' WScript.Arguments(0) is the TARGETDIR on installation
aECs.Add mapName, WScript.Arguments(0) + fileName, Legacy_to_from_Unicode, encodingNameLhs, encodingNameRhs, UnicodeEncodingConversion

' for the 'SIL Heb Trans' font, we also want to add an association between that font
' and the encoding name
aECs.AddFont fontNameSILHebTrans, 42, encodingNameLhs
aECs.AddFontMapping mapName, fontNameSILHebTrans, fontNameCharisSIL

Set aECs = Nothing
