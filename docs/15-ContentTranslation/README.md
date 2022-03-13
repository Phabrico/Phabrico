﻿# Content translation

By default, Phabrico contains some basic multilingual functionality.
By installing the PhrictionTranslator plugin, you can also have translated copies of your Phriction documents.
These translated copies are local copies and will not be uploaded to Phabricator.

The translations themselves are performed by [DeepL](https://www.deepl.com) and are stored in a separate database file (i.e. phabico.translation).
The translations can still be manually edited afterwards.

## Quick guide
Select the document you want to translate and click on *Translate* in the Actions menu:

![ContentTranslation-01](ContentTranslation-01.png) <br />

A dialog will popup in which you enter the translation parameters:

![ContentTranslation-02](ContentTranslation-02.png) <br />

| Parameter | Description
| --- | ---
| Translation engine | The name of the translation service. Currently only DeepL (Free) is implemented.
| API Key            | The API key for authenticating to the translation service
| Source language    | The language in which the document is currently written (and how it appears in Phabricator)
| Target language    | The language in which the document should be translated to

After you click *OK*, you might be asked if you also want to translate the underlying documents:

![ContentTranslation-03](ContentTranslation-03.png) <br />

Just before the translation starts, a warning is shown that you should not send sensitive information over the internet:

![ContentTranslation-04](ContentTranslation-04.png) <br />

When everything is translated, a message is shown:

![ContentTranslation-05](ContentTranslation-05.png) <br />

If you change the language to the one you have just translated into, you will see the translated content:

![ContentTranslation-06](ContentTranslation-06.png) <br />

![ContentTranslation-07](ContentTranslation-07.png) <br />

## Manual editing
Since the translation was performed by machine, some texts may have been translated incorrectly.
You can correct the translation yourself via *Edit translation*

![ContentTranslation-09](ContentTranslation-09.png) <br />

The editor is similar as the original Phriction editor:

![ContentTranslation-10](ContentTranslation-10.png) <br />

| Button | Description
| --- | ---
| Save Changes                   | Your modifications will be stored in the database
| Cancel                         | Your modifications will be not be stored in the database
| Revert to original translation | All your translation modifications on this document will be discarded

## Proofreading
You can also read both the master document and the translation document next to each other via *Proofreading* (or *Vertaling reviseren* in Dutch):

![ContentTranslation-16](ContentTranslation-16.png) <br />

On the left side, you will see the original document.
On the right side, you will the translation.

![ContentTranslation-17](ContentTranslation-17.png) <br />

The slider in the middle can be moved to left or right, if needed.

On top of the screen, you have a toolbar with one button: 

![ContentTranslation-18](ContentTranslation-18.png) <br />

If you click on this button, you will see the Remarkup content of both versions.
The translation can be modified.

![ContentTranslation-19](ContentTranslation-19.png) <br />

## Approval of a translation
After a document is translated, the translation will be marked as 'unreviewed'.
This is visualized by means of a 'big blue world icon' in front of the document's title:

![ContentTranslation-11](ContentTranslation-11.png) <br />

All unreviewed translation can be seen via *Unreviewed translations*:

![ContentTranslation-12](ContentTranslation-12.png) <br />

![ContentTranslation-13](ContentTranslation-13.png) <br />

If you click on a title, the unreviewed translation will be loaded.

If you want to remove a translation completely, you should click on the *Undo* button.

> ⚠️ The *Unreviewed translations* are only shown for the current language

To remove a translation from this *Unreviewed translations* list, you need to approve the translation:

![ContentTranslation-14](ContentTranslation-14.png) <br />

The 'big blue world icon' will also disappear:

![ContentTranslation-15](ContentTranslation-15.png) <br />


> ⚠️ Once you have approved a translation, you cannot undo it afterwards

## Some remarks
* Not all content is automatically translated. Codeblocks, usernames and project names, for example, will not be translated.
* The titles of Notification blocks are only translated if configured under *UI Controls*: 
 ![ContentTranslation-08](ContentTranslation-08.png) <br />
* Diagrams are not automatically translated, but a copy is made for each of them so that you can translate them manually. These diagram copies are also marked as 'unreviewed' and can also be approved in the *Diagrams* screen by means of the green button: ![ContentTranslation-20](ContentTranslation-20.png) <br /> 
The copies of these translated diagrams can be undone via the *Offline Changes* screen.
If you *undo* a diagram, the translated document will link back to the original diagram.
This way you can create diagrams which are the same for all languages.


[Index](../README.md) | [Previous Page](../14-CommandLineInterfacing/README.md)