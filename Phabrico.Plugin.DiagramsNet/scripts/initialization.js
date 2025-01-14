﻿var url = 'diagrams.net/webapp/?embed=1&noSaveBtn=1&spin=1&modified=unsavedChanges&proto=json';
var source = evt.srcElement || evt.target;

if (source.nodeName == 'IMG' && source.className == 'drawio')
{
    if (source.drawIoWindow == null || source.drawIoWindow.closed)
    {
        // Implements protocol for loading and exporting with embedded XML
        var receive = function(evt)
        {
            if (evt.data.length > 0 && evt.source == source.drawIoWindow)
            {
                var msg = JSON.parse(evt.data);

                // Received if the editor is ready
                if (msg.event == 'init')
                {
                    // Sends the data URI with embedded XML to editor
                    source.drawIoWindow.postMessage(JSON.stringify(
                    {
                        action: 'load', 
                        xmlpng: source.getAttribute('src')
                    }), '*');
            }
                // Received if the user clicks save
            else if (msg.event == 'save')
            {
                // Sends a request to export the diagram as XML with embedded PNG
                source.drawIoWindow.postMessage(JSON.stringify(
                    {
                        action: 'export', 
                        format: 'xmlpng', 
                        spinKey: 'saving'
                    }), '*');
            }
                // Received if the export request was processed
            else if (msg.event == 'export')
            {
                // Updates the data URI of the image
                source.setAttribute('src', msg.data);
            }

            // Received if the user clicks exit or after export
            if (msg.event == 'exit' || msg.event == 'export')
            {
                // Closes the editor
                window.removeEventListener('message', receive);
                source.drawIoWindow.close();
                source.drawIoWindow = null;
            }
        }
    };

    // Opens the editor
    window.addEventListener('message', receive);
    source.drawIoWindow = window.open(url);
}
