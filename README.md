# About
Phabrico is a local webserver which acts as an offline editor for [Phabricator's](https://www.phacility.com/phabricator/) Maniphest tasks and Phriction documents.
A partial copy of Phabricator's database will be stored in a local [sqlite](https://www.sqlite.org/index.html) file.
This way you can always access your Maniphest tasks and Phriction documents.

You can also edit your offline copies and synchronize them later to Phabricator.

# License
The source code in this repo is licensed under the Apache v2.

# Running
Phabrico runs under Windows. An MSI setup file is available for download and will install a background service which acts as a HTTP webserver.

To see Phabrico in action, just browse to http://localhost:13467  (TCP port can be customized in MSI setup).

When you browse to Phabrico for the first time, you will be asked to enter a username, a password, Phabricator's web address and your Conduit API token.

The username and password don't need to be the same as the ones used for Phabricator: these credentials will only be used for Phabrico.

The Conduit API token can be found in Phabricator under *Settings* (under your personal icon at the top of the screen) -> *Sessions and logs* -> *Conduit API tokens* -> *Token*

# Supported Browsers
Chrome 89+, Firefox 87+, Opera 74+ and Edge 89+.

# Plugins
Some extra functionality can be added to Phabrico:

## DiagramsNet
integrates the [Diagrams.net (formerly known as draw.io)](https://www.diagrams.net/) functionality.
It allows you to create and modify diagrams. The diagrams are saved as PNG images in Phabricator and can still be modified after being downloaded from Phabricator.

## Gitanos
is a management system for local git repositories.
It visualizes the state of all available local git repositories.

## JSPaint Image Editor
integrates the [JSPaint](https://jspaint.app/) functionality.
It allows you to modify images.

## PhrictionSearch
allows you to search for a word in a Phriction document, including all the documents under this Phriction document.

## PhrictionToPDF
allows you to convert a Phriction document (or including all the documents under this Phriction document) into a single PDF file.

## PhrictionTranslator
creates an offline translation copy of a Phriction document (or including all documents under this Phriction document).
Translation can be performed by means of an Excel file or online by DeepL Free. To use this plugin with DeepL, you need to sign
up for an API key: https://www.deepl.com/pro-api

## PhrictionValidator
verifies if all links to documents or files in a Phriction document (or including all documents under this Phriction document) are valid.

# User manual
See https://phabrico.github.io/Phabrico/


