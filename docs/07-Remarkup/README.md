# Remarkup

Phabricator and Phabrico use a lightweight markup language called "Remarkup", similar to
other lightweight markup languages like Markdown and Wiki markup.

## General

The editors in Phriction and Maniphest contain a code editor on the left and a WYSIWYG result on the right:

![image-20210412153147182](Remarkup-01.png)<br />

The toolbar on top of the code editor contains the following formatting actions:

| Icon                                        | Description                                                  |
| ------------------------------------------- | ------------------------------------------------------------ |
| ![image-20210412153147183](Remarkup-02.png) | Formats the (selected) text in bold                          |
| ![image-20210412153147184](Remarkup-03.png) | Formats the (selected) text in italic                        |
| ![image-20210412153147185](Remarkup-04.png) | Formats the (selected) text in monospaced text               |
| ![image-20210412153147186](Remarkup-05.png) | Creates a bulleted list<br />For example:<br />![image-20210412161052374](Remarkup-11.png) |
| ![image-20210412153147187](Remarkup-06.png) | Creates a bulleted list<br />For example:<br />![image-20210412161052375](Remarkup-12.png) |
| ![image-20210412153147188](Remarkup-07.png) | Creates a code block<br />For example:<br />![image-20210412161052376](Remarkup-13.png) |
| ![image-20210412153147189](Remarkup-08.png) | Creates a quote block<br />For example:<br />![image-20210412161052377](Remarkup-14.png) |
| ![image-20210412153147190](Remarkup-09.png) | Creates a table                                              |
| ![image-20210412153147191](Remarkup-10.png) | Creates a diagram.<br />You need to have the Diagrams plugin installed for this |

At the right end you find the book icon which explains the Remarkup syntax more in detail

## Drag and drop functionalities

Files can be drag-and-dropped into the code editor area:<br />
![image-20210412153147100](Remarkup-15.png) <br />
The code editor area will color green until you dropped the file.
If the file is an image, the image will be shown as is in the right WYSIWYG area.
If the file is not an image (e.g. a PDF file), an icon representing the file be shown. <br />
For example:<br />
![image-20210412153147101](Remarkup-16.png) <br /><br />

Audio and video files will also directly visualized. <br />
For example: <br />
![image-20210412153147102](Remarkup-17.png) <br />



## Copy / Paste functionalities

Table data from Microsoft Excel can be directly copy pasted if the table does not contain merged cells.
Color and formatting aren't copied.
However, a formatted Excel cell will be seen as header cell in Remarkup (it will be visualized bold)<br />
For example: <br />
![image-20210412153147103](Remarkup-19.png) <br />
![image-20210412153147104](Remarkup-18.png) <br />

[Index](../README.md) | [Previous Page](../06-Phriction/README.md) |  [Next page](../08-OfflineChanges/README.md)