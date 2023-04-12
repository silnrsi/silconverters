# SIL Converters
This package provides tools through which you can change the encoding, font, and/or script of text in Microsoft Word and other Office documents, OpenOffice and LibreOffice documents, XML documents, and SFM text and lexicon documents. It also installs a system-wide repository to manage your encoding converters and transliterators (TECkit, CC, ICU, Perl, or Python based, as well as support for adding custom transduction engines) and now several web-based translators (Bing Translate, DeepL Translate, and Google Translate).

For developers, it provides a simple COM interface to select and use a converter from the repository. It is easy to use from VBA, C++, C#, Perl, Python or any .NET/COM enabled language.

The core EncConverters assembly is fully integrated with FLEx (FieldWorks Language Explorer), Speech Analyzer, Phonology Assistant, Adapt It, OneStory Editor software, and now there's a Paratext Plug-in also. It provides the same system-wide registry of installed and available encoding converters for all of these user programs. Additionally the package includes some extra utilities such as a clipboard converter for manipulating text between cut and paste operations.

The following picture illustrates the suite of tools, utilities, and applications that are available and how they interact:

![alt text](https://software.sil.org/wp/wp-content/uploads/2021/07/silconvertersFig1.jpg)
Figure 1. SIL Converters Suite

Figure 1 shows the three distinct layers to SIL Converters.

  * At the top are various client applications. These user-oriented programs use the [EncConverters core](https://github.com/silnrsi/encoding-converters-core) assembly to provide encoding conversion and other transduction facilities to their users.
  * The EncConverters core provides an abstraction layer so the client applications can access the various transduction engines without having to implement the interface to each one separately.
  * The transduction engines are the server applications that provide the actual conversion/text processing capability.

If you are an end user, you are probably most interested in how to use EncConverters with client applications—for example:

  * Using the Bulk Word Document Converter to convert the encoding of text in one or more Word document to Unicode, or
  * Using Bulk SFM Converter to convert SFM documents into Unicode (typically texts and lexicons from Shoebox to Toolbox)
  * Using the Paratext plug-in to help with Back Translation (generally using one or more of the Translator EncConverters to get suggestions)

If you are a developer, you may be interested in

  * Using EncConverters to gain access to the different transduction resources available by writing to the single EncConverters’ interface. See [this](https://software.sil.org/silconverters/silconverters-developer/) webpage for details and code snippets.

## Upgrade! New Features
### For version 5.1
  * Support for the new web-based Translation resources (Bing, DeepL, and Google Translate)
  * Support 64 bit version of Microsoft Office, as well as continued support for 32 bit versions. See the documentation below for how to determine which bitness of Microsoft Office you have, and therefore which installer you should download.
  * Support newer versions of Microsoft Office (including 2019 and 365).
  * TECkit mapping editor can now show characters outside of the Basic Multilingual Plane (BMP), that is, Unicode characters above U+FFFF.
  * Updated TECkit maps.
  * Updated TECkit to support Unicode 13.

See also [this](https://software.sil.org/silconverters) page for more information and a signed installer download

For building SILConverters (currently only on Windows), open the solution in Visual Studio in administrative mode, and click Rebuild All on the Build menu. 

If you want to upgrade the EncConverters core nuget package, it is recommended to follow these steps rather than using the normal Manage Nuget packages approach (it doesn't update the package properly for x86 vs. x64 builds or use the pre-defined 'EcLibFilesPath' nuget property for the path to the new nuget package):
  * With the Visual Studio Solution closed, use something like the 'Search', 'Find in Files' feature in Notepad++ to search these files and make replacements:
     * Directory: <location of the solution folder> (e.g. C:\Users\<username>\source\repos\SilConverters\)
	 * Check the 'In all sub-folders' checkbox
	 * Search Mode: Normal

     * Filters: packages.config
	 * Find What: Encoding-Converters-Core" version="0.6.0
	 * Replace With: Encoding-Converters-Core" version="0.6.1
	 * Click the 'Replace in Files' button
	 
     * Filters: *.*proj
	 * Find What: packages\Encoding-Converters-Core.0.6.0
	 * Replace With: packages\Encoding-Converters-Core.0.6.1
	 * Click the 'Replace in Files' button
	 
  * With File Explorer, delete these folders:
     * <solution directory>\build\Obj
	 * <solution directory>\output\x86, x64, Release, Debug
	 * <solution directory>\packages\Encoding-Converters-Core
	 
  * Open Solution in Visual Studio, right-click on the Solution in the Solution Explorer and choose "Restore Nuget packages"
  * Close the solution (File, Close Solution) and reopen it (don't skip this step!)
  * Right-click the Solution in the Solution Explorer and choose 'Clean Solution' and then 'Rebuild All' (do this for each configuration you want)
  * Run the tests to make sure everything is working as expected.
