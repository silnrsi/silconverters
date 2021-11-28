# SIL Converters
This package provides tools through which you can change the encoding, font, and/or script of text in Microsoft Word and other Office documents, OpenOffice and LibreOffice documents, XML documents, and SFM text and lexicon documents. It also installs a system-wide repository to manage your encoding converters and transliterators (TECkit, CC, ICU, Perl, or Python based, as well as support for adding custom transduction engines).

For developers, it provides a simple COM interface to select and use a converter from the repository. It is easy to use from VBA, C++, C#, Perl, Python or any .NET/COM enabled language.

The core EncConverters assembly is fully integrated with FLEx (FieldWorks Language Explorer), Speech Analyzer, Phonology Assistant, Adapt It and OneStory Editor software. It provides the same system-wide registry of installed and available encoding converters for all of these user programs. Additionally the package includes some extra utilities such as a clipboard converter for manipulating text between cut and paste operations.

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

If you are a developer, you may be interested in

  * Using EncConverters to gain access to the different transduction resources available by writing to the single EncConverters’ interface. See [this](https://software.sil.org/silconverters/silconverters-developer/) webpage for details and code snippets.

## Upgrade! New Features
### For version 5.0
  * Support 64 bit version of Microsoft Office, as well as continued support for 32 bit versions. See the documentation below for how to determine which bitness of Microsoft Office you have, and therefore which installer you should download.
  * Support newer versions of Microsoft Office (including 2019 and 365).
  * TECkit mapping editor can now show characters outside of the Basic Multilingual Plane (BMP), that is, Unicode characters above U+FFFF.
  * Updated TECkit maps.
  * Updated TECkit to support Unicode 13.

See also [this](https://software.sil.org/silconverters) page for more information and a signed installer download
