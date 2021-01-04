1) rename: <solutionDir>\output\SetupEC.msi to be: SEC Setup.msi
2) put "SEC Setup.msi" into: \\cana\LSDev\Installers\SEC 4.0 Web\SIL Converters
3) open "D:\Work\Master Installer\Installer Definitions\SEC 4.0 Web\SEC 4.0 Web.xml" in IE
4) press F12 and set to IE8 compatibility mode
5) in the resulting webpage (title: Web Downloads Configuration), enter password (get from Ann) and click 'build'
6) result should be in: G:\Software Package Builder\Web Downloads\SEC 4.0 Web
7) check for md5 checksum (you need them for the website).
8) Ann will put the two packages, SEC_FullInstall.exe and SEC_PackageOnly.exe in the folder that results in the web path: "http://downloads.sil.org/EncodingConverters/SEC 4.0 Web"
9) update the md5 values in the http://scripts.sil.org/EncCnvtrs website:
  a) Log in to SSO (http://scripts.sil.org/cms/scripts/mypage.php?site_id=nrsi). See Lorna Evans for username/password
  b) Go to this page: http://scripts.sil.org/EncCnvtrs
  c) At the top you will see an "Edit this item" link. click that and in the resulting window
  d) Fix the md5 values
