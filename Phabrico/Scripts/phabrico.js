const phabrico = {};

// ************************************************************************************************
class AcceleratorKeys {
    constructor() {
        var instance = this;

        this.doProcessKey = function(evt) {
            // check if focused element is not a textbox, passwordbox or a textarea field
            if ( Array.prototype.slice.call( document.querySelectorAll('input[type=text],input[type=password],textarea'), 0 ).indexOf( document.activeElement ) )
            {
                var button = Array.prototype.slice.call(document.querySelectorAll('button,a', 0))
                                  .find( function(elem) {
                                      return (!!( elem.offsetWidth || elem.offsetHeight || elem.getClientRects().length )) &&
                                              elem.dataset.accesskey == evt.key.toUpperCase();
                });

                if (button) {
                    button.click();
                }
            }
        }

        document.body.addEventListener('keydown', instance.doProcessKey);
    }
}

// ************************************************************************************************
class AppSideWindow {
    constructor() {
        this.Collapse = function() {
            if (this.pageBody !== null) {
                this.pageBody.classList.add('right-collapsed');

                document.querySelectorAll('div.codeblock button.codeblock.copy').forEach((btn) => {
                    btn.classList.remove('overlapped');
                });

                localStorage["phabrico-page-content-collapsed"] = "1";
            }
        }

        this.Expand = function() {
            if (this.pageBody !== null) {
                this.pageBody.classList.remove('right-collapsed');

                document.querySelectorAll('div.codeblock button.codeblock.copy').forEach((btn) => {
                    if (isElementBehindAppSideWindow(btn)) {
                        btn.classList.add('overlapped');
                    }
                });

                localStorage["phabrico-page-content-collapsed"] = "";
            }
        }
    }

    get pageBody() {
        return document.querySelector(".phabrico-page-content");
    }
}

// ************************************************************************************************
class AutoLogOff {
    constructor(autoLogOutAfterMinutesOfInactivity)
    {
        var instance = this;
        var timeoutTime = autoLogOutAfterMinutesOfInactivity * 60000;
        var keepAliveTime = 10000;
        var isEnabled = true;

        if (timeoutTime == 0)
        {
            timeoutTime = 2147483647;
        }

        instance.timeoutTimer = null;
        instance.keepAliveTimer = null;

        this.doLogOff = function(evt) {
            var xmlhttp = new XMLHttpRequest();
            xmlhttp.open('GET', document.baseURI + 'logout', true);
            xmlhttp.setRequestHeader('Content-type', 'multipart/form-data; charset=utf-8');
            xmlhttp.onload = function () {
                window.location.reload();
            };
            xmlhttp.send();
        }

        this.doPoke = function (evt) {
            if (instance.isEnabled) {
                var pokeRequest = new XMLHttpRequest();
                pokeRequest.onreadystatechange = function () {
                    if (pokeRequest.readyState == 4 && pokeRequest.status == 200) {
                        try {
                            var jsonResponse = JSON.parse(pokeRequest.responseText);
                        }
                        catch (exc) {
                            // response should be JSON; if it's something else (e.g. HTML), load homepage
                            console.log(exc);
                            document.location = document.location.href.split('?')[0];  // reload page -> server will redirect to login dialog and remember the current url (for redirection after authenticated)
                        }
                    }
                };
                pokeRequest.open('GET', document.baseURI + 'poke', true);
                pokeRequest.setRequestHeader('Content-type', 'multipart/form-data; charset=utf-8');
                pokeRequest.send();
            }
        }

        this.doWakeUp = function(evt) {
            if (instance.isEnabled) {
                clearTimeout(instance.timeoutTimer);
                instance.timeoutTimer = setTimeout(instance.doLogOff, timeoutTime);
            }
        }

        this.disable = function() {
            instance.isEnabled = false;
            document.body.removeEventListener('mousemove', instance.doWakeUp);
            document.body.removeEventListener('mousedown', instance.doWakeUp);
            document.body.removeEventListener('keydown', instance.doWakeUp);
            document.body.removeEventListener('touchstart', instance.doWakeUp);
            document.body.removeEventListener('wheel', instance.doWakeUp);
            clearTimeout(instance.timeoutTimer);

            // inform server that token should not be invalidated after no poke message was sent for 120 seconds
            var disablePokeRequest = new XMLHttpRequest();
            disablePokeRequest.open('POST', document.baseURI + 'poke', true);
            disablePokeRequest.setRequestHeader('Content-type', 'multipart/form-data; charset=utf-8');
            var data = new FormData();
            data.append('enabled', 0);
            disablePokeRequest.send(data);
        }

        this.enable = function() {
            instance.isEnabled = true;
            document.body.addEventListener('mousemove', instance.doWakeUp);
            document.body.addEventListener('mousedown', instance.doWakeUp);
            document.body.addEventListener('keydown', instance.doWakeUp);
            document.body.addEventListener('touchstart', instance.doWakeUp);
            document.body.addEventListener('wheel', instance.doWakeUp);
            instance.timeoutTimer = setTimeout(instance.doLogOff, timeoutTime);
        }

        instance.enable();
        instance.doWakeUp();

        instance.keepAliveTimer = setInterval(instance.doPoke, keepAliveTime);
    }
}

// ************************************************************************************************
class CrumbsHeader {
    constructor(crumbsContainer, title, crumbs) {
        var lastCrumbObject = crumbsContainer.firstElementChild;
        var slug = lastCrumbObject.href;

        var a = document.createElement('a');
        a.href = slug;
        a.innerText = title;
        lastCrumbObject.insertAdjacentElement('afterEnd', a);
        lastCrumbObject = a;

        for (var crumb in crumbs) {
            if (crumbs[crumb].slug == '') continue;

            var span = document.createElement('span');
            span.innerText = '  >  ';
            lastCrumbObject.insertAdjacentElement('afterEnd', span);
            lastCrumbObject = span;

            a = document.createElement('a');
            slug += '/' + crumbs[crumb].slug;
            a.href = slug + '/';
            a.innerText = crumbs[crumb].name;
            if (crumbs[crumb].hidden) {
                a.classList.add('hidden');
                span.classList.add('hidden');
            } else
            if (crumbs[crumb].inexistant) {
                a.style.pointerEvents = 'none';
            }

            lastCrumbObject.insertAdjacentElement('afterEnd', a)
            lastCrumbObject = a;
        }
    }
}

// ************************************************************************************************
class InputTag {
    constructor() {
        var me = this;

        this.create = function(input) {
            var maxNbrTags = parseInt(isNaN(input.dataset.limit) ? 999 : 1);
            input.type = 'hidden';
            input.classList = 'hidden-input-tag';

            var tokens = input.value;
            input.value = '';

            var settings = {};

            settings.placeholder = input.placeholder;
            settings.tabIndex = input.tabIndex;
            settings.divInputTag = document.createElement('div');
            settings.divInputTag.classList.add('input-tag');
            input.parentElement.insertBefore(settings.divInputTag, input);

            var divFocusContainer = document.createElement('div');
            divFocusContainer.onclick = function () {
                divFocusContainer.querySelector('input').focus();
            };
            settings.divInputTag.appendChild(divFocusContainer);

            if (maxNbrTags == 1) {
                divFocusContainer.style.height = '30px';
            }

            var inputText = document.createElement('input');
            inputText.type = 'text';
            inputText.autocomplete = 'off';
            inputText.value = '';
            inputText.settings = settings;
            me.setInputTextSettings(inputText);
            divFocusContainer.appendChild(inputText);

            var divMenu = document.createElement('div');
            divMenu.classList.add('menu');
            divMenu.style.zIndex = 99;
            divMenu.style.position = 'absolute';
            divMenu.style.top = '33px';
            divMenu.style.left = '100px';
            divMenu.style.width = 'calc(100% - 106px)';
            settings.divInputTag.appendChild(divMenu);

            inputText.onkeydown = function () {
                return me.inputTextKeyDown(this, event);
            };
            inputText.onkeyup = function () {
                return me.inputTextKeyUp(this, event);
            };
            inputText.onblur = function () {
                divFocusContainer.classList.remove('focused');

                var newlyFocusedElement = event.relatedTarget || (event.rangeParent && event.rangeParent.parentElement);
                while (newlyFocusedElement != null &&
                       newlyFocusedElement != divMenu &&
                       newlyFocusedElement.classList.contains('input-tag') == false)
                {
                    newlyFocusedElement = newlyFocusedElement.parentElement;
                }

                if (newlyFocusedElement != divMenu &&
                    divMenu.classList.contains('opened') &&
                    divMenu.children.length > 0)
                {
                    inputText.value = '';
                    divMenu.classList.remove('opened');
                }
            };
            inputText.onfocus = function () {
                divFocusContainer.classList.add('focused');
            };

            var httpGetTagNames = new XMLHttpRequest();
            var xmlhttpUrl = document.baseURI + input.dataset.url;
            httpGetTagNames.overrideMimeType("application/json");
            httpGetTagNames.open('GET', xmlhttpUrl + "/get/?tokens=" + tokens, true);
            httpGetTagNames.onload  = function() {
                var jsonResponse = JSON.parse(httpGetTagNames.responseText);

                for (var i=0; i < jsonResponse.length; i++)
                {
                    if (input.previousSibling.children[1].children.length > 0 &&
                        maxNbrTags < input.previousSibling.children[1].children[0].children.length - 1) {
                        break;
                    }

                    var tagToken = jsonResponse[i].Token;
                    var tagName = jsonResponse[i].Name;
                    var tagIcon = jsonResponse[i].FontAwesomeIcon;
                    me.addTag(settings.divInputTag, tagName, tagToken, tagIcon);
                }
            };
            httpGetTagNames.send(null);

            settings.divInputTag.inputText = inputText;
            return settings.divInputTag;
        }

        this.addTag = function (divInputTag, name, value, fontAwesomeIcon) {
            var divInputTagFirstChild = divInputTag.children[0];
            var span = document.createElement('span');
            var inputText = divInputTagFirstChild.lastElementChild;
            var inputHidden = divInputTag.nextSibling;
            divInputTagFirstChild.insertBefore(span, inputText);

            var spanBriefCase = document.createElement('span');
            spanBriefCase.className += ' phui-icon-view';
            spanBriefCase.className += ' phui-font-fa';
            spanBriefCase.className += ' ' + fontAwesomeIcon;
            span.appendChild(spanBriefCase);

            var text = document.createTextNode(name + " ");
            span.appendChild(text);

            var input = document.createElement('input');
            input.type = 'hidden';
            input.id = value;
            span.appendChild(input);

            var anchorClose = document.createElement('a');
            anchorClose.onclick = function () {
                var inputTag = this.closest('.input-tag');

                me.delTag(this);

                if (divInputTagFirstChild.children.length == 1) {
                    me.setInputTextSettings(divInputTagFirstChild.firstElementChild);
                }

                if (me.onChanged != null) {
                    me.onChanged(inputTag, inputTag.nextSibling.value);
                }
            };
            anchorClose.innerHTML = '&times;';
            span.appendChild(anchorClose);

            inputText.placeholder = '';

            if (inputHidden.value === "")
            {
                inputHidden.value = value;
            }
            else
            if (inputHidden.value.indexOf("," + value) == -1
                && inputHidden.value.indexOf(value + ",") == -1
                && inputHidden.value !== value)
            {
                inputHidden.value += "," + value;
            }
        }

        this.delTag = function (anchorClose) {
            var divInputTagFirstChild = anchorClose.parentElement.parentElement.parentElement.children[0];
            var inputText = divInputTagFirstChild.lastElementChild;
            var inputHidden = inputText.parentElement.parentElement.nextSibling;
            var token = anchorClose.previousElementSibling.id;

            inputHidden.value = inputHidden.value.replace("," + token, "");
            inputHidden.value = inputHidden.value.replace(token + ",", "");
            inputHidden.value = inputHidden.value.replace(token, "");

            if (divInputTagFirstChild.children.length == 2)
            {
                var inputText = divInputTagFirstChild.lastElementChild;
                me.setInputTextSettings(inputText);
            }
            anchorClose.parentElement.parentElement.removeChild(anchorClose.parentElement);
        }

        this.setInputTextSettings = function (inputText) {
            inputText.placeholder = inputText.settings.placeholder;
            inputText.tabIndex = inputText.settings.tabIndex;
        }

        this.inputTextKeyDown = function (inputText, e) {
            switch (e.keyCode)
            {
                case 8 : // backspace
                    if (inputText.parentElement.children.length > 1 &&
                        inputText.selectionStart == 0)
                    {
                        this.delTag(inputText.previousElementSibling.lastElementChild);

                        if (me.onChanged != null) {
                            me.onChanged(inputTag, inputTag.nextSibling.value);
                        }
                        return;
                    }
                    break;

                case 9 : // Tab
                case 13 : // Enter
                    var inputTag = inputText.parentElement.parentElement;
                    var menu = inputTag.querySelector('.menu.opened');
                    if (menu != null) {
                        var selectedMenuItem = menu.querySelector('a.input-tag-hovered');
                        if (selectedMenuItem != null) {
                            inputText.value = "";
                            this.addTag(inputTag, selectedMenuItem.name, selectedMenuItem.rel, selectedMenuItem.dataset.icon);

                            if (me.onChanged != null) {
                                me.onChanged(inputTag, inputTag.nextSibling.value);
                            }
                        }
                        menu.classList.remove('opened');
                    }
                    break;

                case 27 : // Escape
                    if (this.httpSearchTag != null) {
                        this.httpSearchTag.abort();
                    }
                    var inputHidden = inputText.parentElement.parentElement.parentElement.querySelector('input.hidden-input-tag');
                    var inputTag = inputText.parentElement.parentElement;
                    var menu = inputTag.querySelector('.menu');
                    menu.classList.remove('opened');
                    break;

                case 37 : // Arrow-Left
                case 39 : // Arrow-Right
                    return true;

                case 38 : // Arrow-Up
                    var inputTag = inputText.parentElement.parentElement;
                    var menu = inputTag.querySelector('.menu');
                    var selectedMenuItem = menu.querySelector('a.input-tag-hovered');
                    var previousMenuItem = selectedMenuItem.previousSibling;
                    if (selectedMenuItem != null && previousMenuItem != null) {
                        selectedMenuItem.classList.remove('input-tag-hovered');
                        previousMenuItem.classList.add('input-tag-hovered');
                    }
                    return false;

                case 40 : // Arrow-Down
                    var inputTag = inputText.parentElement.parentElement;
                    var menu = inputTag.querySelector('.menu');
                    var selectedMenuItem = menu.querySelector('a.input-tag-hovered');
                    var nextMenuItem = selectedMenuItem.nextSibling;
                    if (selectedMenuItem != null && nextMenuItem != null) {
                        selectedMenuItem.classList.remove('input-tag-hovered');
                        nextMenuItem.classList.add('input-tag-hovered');
                    }
                    return false;

                default:
                    var hiddenInput =  inputText.parentElement.parentElement.parentElement.querySelector('input.hidden-input-tag');
                    var maxNbrTags = parseInt( isNaN(hiddenInput.dataset.limit) ? 999 : 1);
                    if (hiddenInput.previousSibling.children.length > 0 &&
                        maxNbrTags <=  hiddenInput.previousSibling.children[0].children.length - 1)
                    {
                        return false;
                    }
                    break;
            }

            return true;
        }

        this.inputTextKeyUp = function (inputText, e) {
            if (e.key.length == 1 ||   // is key 'printable' ?
                e.keyCode == 8) {      // is key backspace ?
                this.loadMenuContent(inputText);
            }

            return true;
        }

        this.loadMenuContent = function (inputText) {
            var inputHidden = inputText.parentElement.parentElement.parentElement.querySelector('input.hidden-input-tag');
            var xmlhttpUrl = document.baseURI + inputHidden.dataset.url;
            var inputTag = inputText.parentElement.parentElement;
            var menu = inputTag.querySelector('.menu');

            if (inputText.value == '') {
                menu.innerHTML = '';
                menu.classList.remove('opened');
            } else {
                if (this.httpSearchTag != null) {
                    this.httpSearchTag.abort();
                    menu.innerHTML = '';
                }

                this.httpSearchTag = new XMLHttpRequest();
                var httpSearchTag = this.httpSearchTag;
                this.httpSearchTag.overrideMimeType("application/json");
                this.httpSearchTag.open('GET', xmlhttpUrl + "/search/?keyword=" + inputText.value + "&tags=" + inputHidden.value, true);
                this.httpSearchTag.onload  = function() {
                    var jsonResponse = JSON.parse(httpSearchTag.responseText);

                    for (var i=0; i < jsonResponse.length; i++)
                    {
                        var anchor = document.createElement('a');
                        anchor.onclick = function() {
                            me.addTag(inputTag, this.name, this.rel, this.dataset.icon);
                            inputText.value = '';
                            menu.classList.remove('opened');

                            if (me.onChanged != null) {
                                me.onChanged(inputTag, inputTag.nextSibling.value);
                            }
                        };
                        anchor.onmousemove = function(ev) {
                            // remove 'hover' status from all menu items
                            menu.querySelectorAll('a.input-tag-hovered').forEach(function(item) {
                              item.classList.remove('input-tag-hovered');
                            });

                            // search for the hovered anchor tag
                            var hoveredAnchor;
                            if (ev.target.tagName == 'A') {
                                hoveredAnchor = ev.target;
                            } else {
                                hoveredAnchor = ev.target.closest('A');
                            }

                            // mark anchor tag as 'hovered'
                            hoveredAnchor.classList.add('input-tag-hovered');
                        };
                        anchor.name = jsonResponse[i].Name;
                        anchor.rel = jsonResponse[i].Token;
                        anchor.dataset.icon = jsonResponse[i].FontAwesomeIcon;

                        var div = document.createElement('div');
                        anchor.appendChild(div);

                        var span = document.createElement('span');
                        span.classList.add('phui-icon-view');
                        span.classList.add('phui-font-fa');
                        span.classList.add(jsonResponse[i].FontAwesomeIcon);
                        div.appendChild(span);

                        var text = document.createTextNode(jsonResponse[i].Name);
                        div.appendChild(text);

                        menu.appendChild(anchor);
                    }

                    if (menu.firstElementChild != null) {
                        menu.firstElementChild.classList.add('input-tag-hovered')
                    }

                    menu.classList.add('opened');

                    setTimeout(function() {
                                    if (menu.querySelectorAll('a:hover').length > 0) {
                                        menu.firstElementChild.classList.remove('input-tag-hovered');
                                    }
                               },
                               100);

                    this.httpSearchTag = null;
                };
                this.httpSearchTag.send(null);
            }
        }

        this.onChanged = null;

        var inputTagInputs = document.querySelectorAll('input.input-tag');
        for (var i=0; i<inputTagInputs.length; i++) {
            var divInputTag = this.create(inputTagInputs[i]);
            this.setInputTextSettings(divInputTag.firstElementChild.firstElementChild);
        };
    }
}

// ************************************************************************************************
class ProgressBar {
    constructor() {
        // initialize AJAX object
        const xmlhttp = new XMLHttpRequest();
        this.xmlhttp = xmlhttp;
        var me = this;
        this.xmlhttp.onreadystatechange = function ()
        {
            if (xmlhttp.readyState == 4 && xmlhttp.status == 200) {
                try {
                    var progress = JSON.parse(xmlhttp.responseText);
                    me.currentValue = progress.Percentage;
                    me.text = progress.Description;
                    me.status = progress.Status;
                    me.stackTrace = progress.StackTrace;

                    if (me.currentValue < 100) {
                        var tmrProgressBar = setTimeout(function(e) {
                            me.xmlhttp.open("GET", me.URL, true);
                            me.xmlhttp.send();

                            if (me.currentValue == 100) {
                                clearInterval(tmrProgressBar);
                            }
                        }, 250);
                    }
                }
                catch(exc) {
                    document.location = document.baseURI;
                }
            }
        };

        this.tmrDecodeRemarkup = null;
    }

    show(divContainer,url) {
        divContainer.innerHTML = "";
        divContainer.classList.add('progress-bar');

        var divBorder = document.createElement('div');
        divContainer.appendChild(divBorder);

        var divProgress = document.createElement('div');
        divProgress.style.width = "0%";
        divBorder.appendChild(divProgress);

        var spanProgress = document.createElement('div');
        divProgress.innerHTML = "&nbsp;";
        divProgress.appendChild(spanProgress);

        var spanText = document.createElement('span');
        divContainer.appendChild(spanText);

        this.currentValue = 0;
        this.text = '';

        var me = this;

        me.URL = url;

        // execute initial Ajax call with a small delay
        setTimeout(function(){
            me.xmlhttp.open("GET", url, true);
            me.xmlhttp.send();
        }, 300);
    }

    set currentValue(val){
        if (val < 0) val = 0;
        if (val > 100) val = 100;
        document.querySelector('.progress-bar div div').style.width = val + '%';
    }

    get currentValue(){
        return parseInt(document.querySelector('.progress-bar div div').style.width);
    }

    set text(val){
        document.querySelector('.progress-bar span').innerText = val;
    }

    get text(){
        return document.querySelector('.progress-bar span').innerText;
    }
}

// ************************************************************************************************
class Remarkup {
    constructor(onChanged) {
        // initialize AJAX object
        const xmlhttp = new XMLHttpRequest();
        this.xmlhttp = xmlhttp;
        this.xmlhttp.onreadystatechange = function ()
        {
            if (xmlhttp.readyState == 4 && xmlhttp.status == 200) {
                var htmlData = JSON.parse(xmlhttp.responseText).html;
                xmlhttp.destinationField.innerHTML = htmlData;

                document.querySelectorAll('pre code').forEach((block) => {
                    hljs.highlightBlock(block);
                });

                var table = new Table();
                table.conceal( xmlhttp.destinationField );

                correctHeaderHeights();

                finishHyperlinks();
                finishMetaDataElements();
            }

            if (onChanged != null) {
                onChanged();
            }
        };

        this.tmrDecodeRemarkup = null;

        document.querySelectorAll('.edit .app-edit-window-head > .phui-font-fa').forEach((toolbarButton) => {
            toolbarButton.onclick = function(ev) {
                var span = ev.target;
                var textarea = document.querySelector('.app-window-body.edit > textarea');
                var selectedText = textarea.Text.substring(textarea.selectionStart, textarea.selectionEnd);
                var selectionStart = textarea.selectionStart;
                var selectionEnd = textarea.selectionEnd;

                if (span.classList.contains('fa-bold'))
                {
                    if (selectedText == "") {
                        selectedText = Locale.Translate("Bold text");
                    }

                    if(selectedText.startsWith("**") && selectedText.endsWith("**")) {
                        // already in bold -> undo bold
                        textarea.Text = textarea.Text.substring(0, selectionStart)
                                        + selectedText.substr(2, selectedText.length - 4)
                                        + textarea.Text.substring(selectionEnd);
                        textarea.focus();
                        textarea.selectionStart = selectionStart;
                        textarea.selectionEnd = textarea.selectionStart + selectedText.length - 4;
                    } else if(selectionStart > 2  &&  textarea.Text.substr(textarea.selectionStart - 2, 2) == "**" && textarea.Text.substring(textarea.selectionEnd).startsWith("**")) {
                        // already in bold -> undo bold
                        textarea.Text = textarea.Text.substring(0, selectionStart - 2)
                                        + selectedText
                                        + textarea.Text.substring(selectionEnd + 2);
                        textarea.focus();
                        textarea.selectionStart = selectionStart - 2;
                        textarea.selectionEnd = textarea.selectionStart + selectedText.length;
                    } else {
                        // set to bold
                        textarea.Text = textarea.Text.substring(0, selectionStart)
                                        + "**" + selectedText + "**"
                                        + textarea.Text.substring(selectionEnd);
                        textarea.focus();
                        textarea.selectionStart = selectionStart + 2;
                        textarea.selectionEnd = textarea.selectionStart + selectedText.length;
                    }
                    return;
                }

                if (span.classList.contains('fa-italic'))
                {
                    if (selectedText == "") {
                        selectedText = Locale.Translate("Italic text");
                    }

                    if(selectedText.startsWith("//") && selectedText.endsWith("//")) {
                        // already in italic -> undo italic
                        textarea.Text = textarea.Text.substring(0, selectionStart)
                                        + selectedText.substr(2, selectedText.length - 4)
                                        + textarea.Text.substring(selectionEnd);
                        textarea.focus();
                        textarea.selectionStart = selectionStart;
                        textarea.selectionEnd = textarea.selectionStart + selectedText.length - 4;
                    } else if(selectionStart > 2  &&  textarea.Text.substr(textarea.selectionStart - 2, 2) == "//" && textarea.Text.substring(textarea.selectionEnd).startsWith("//")) {
                        // already in italic -> undo italic
                        textarea.Text = textarea.Text.substring(0, selectionStart - 2)
                                        + selectedText
                                        + textarea.Text.substring(selectionEnd + 2);
                        textarea.focus();
                        textarea.selectionStart = selectionStart - 2;
                        textarea.selectionEnd = textarea.selectionStart + selectedText.length;
                    } else {
                        // set to italic
                        textarea.Text = textarea.Text.substring(0, selectionStart)
                                    + "//" + selectedText + "//"
                                    + textarea.Text.substring(selectionEnd);
                        textarea.focus();
                        textarea.selectionStart = selectionStart + 2;
                        textarea.selectionEnd = textarea.selectionStart + selectedText.length;
                    }
                    return;
                }

                if (span.classList.contains('fa-text-width'))
                {
                    if (selectedText == "") {
                        selectedText = Locale.Translate("Monospaced text");
                    }


                    if(selectedText.startsWith("`") && selectedText.endsWith("`")) {
                        // already monospaced -> undo monospaced
                        textarea.Text = textarea.Text.substring(0, selectionStart)
                                        + selectedText.substr(1, selectedText.length - 2)
                                        + textarea.Text.substring(selectionEnd);
                        textarea.focus();
                        textarea.selectionStart = selectionStart;
                        textarea.selectionEnd = textarea.selectionStart + selectedText.length - 2;
                    } else if(selectionStart > 1  &&  textarea.Text.substr(textarea.selectionStart - 1, 1) == "`" && textarea.Text.substring(textarea.selectionEnd).startsWith("`")) {
                        // already monospaced -> undo monospaced
                        textarea.Text = textarea.Text.substring(0, selectionStart - 1)
                                        + selectedText
                                        + textarea.Text.substring(selectionEnd + 1);
                        textarea.focus();
                        textarea.selectionStart = selectionStart - 1;
                        textarea.selectionEnd = textarea.selectionStart + selectedText.length;
                    } else {
                        // set to monospaced
                        textarea.Text = textarea.Text.substring(0, selectionStart)
                                    + "`" + selectedText + "`"
                                    + textarea.Text.substring(selectionEnd);
                        textarea.focus();
                        textarea.selectionStart = selectionStart + 1;
                        textarea.selectionEnd = textarea.selectionStart + selectedText.length;
                    }
                    return;
                }

                if (span.classList.contains('fa-list-ul'))
                {
                    if (selectedText == "") {
                        selectedText = "List Item";
                    }

                    textarea.Text = textarea.Text.substring(0, selectionStart)
                                    + "\n\n  - " + selectedText
                                    + textarea.Text.substring(selectionEnd);
                    textarea.focus();
                    textarea.selectionStart = selectionStart + 6;
                    textarea.selectionEnd = textarea.selectionStart + selectedText.length;
                    return;
                }

                if (span.classList.contains('fa-list-ol'))
                {
                    if (selectedText == "") {
                        selectedText = "List Item";
                    }

                    textarea.Text = textarea.Text.substring(0, selectionStart)
                                    + "\n\n  # " + selectedText
                                    + textarea.Text.substring(selectionEnd);
                    textarea.focus();
                    textarea.selectionStart = selectionStart + 6;
                    textarea.selectionEnd = textarea.selectionStart + selectedText.length;
                    return;
                }

                if (span.classList.contains('fa-code'))
                {
                    if (selectedText == "") {
                        selectedText = "foreach ($list as $item) {\n  work_miracles($item);\n}";
                    }

                    textarea.Text = textarea.Text.substring(0, selectionStart)
                                    + "\n```\n" + selectedText + "\n```\n"
                                    + textarea.Text.substring(selectionEnd);
                    textarea.focus();
                    textarea.selectionStart = selectionStart + 5;
                    textarea.selectionEnd = textarea.selectionStart + selectedText.length;
                    return;
                }

                if (span.classList.contains('fa-quote-right'))
                {
                    if (selectedText == "") {
                        selectedText = "Quoted Text";
                    }

                    textarea.Text = textarea.Text.substring(0, selectionStart)
                                    + "\n> " + selectedText + "\n\n"
                                    + textarea.Text.substring(selectionEnd);
                    textarea.focus();
                    textarea.selectionStart = selectionStart + 3;
                    textarea.selectionEnd = textarea.selectionStart + selectedText.length;
                    return;
                }

                if (span.classList.contains('fa-table'))
                {
                    if (selectedText == "") {
                        selectedText = "Data";
                    }

                    textarea.Text = textarea.Text.substring(0, selectionStart)
                                    + "\n| " + selectedText + " |"
                                    + textarea.Text.substring(selectionEnd);
                    textarea.focus();
                    textarea.selectionStart = selectionStart + 3;
                    textarea.selectionEnd = textarea.selectionStart + selectedText.length;
                    return;
                }

                if (span.classList.contains('fa-sitemap'))
                {
                    sessionStorage["remarkup-editor-text-before"] = textarea.Text.substring(0, selectionStart);
                    sessionStorage["remarkup-editor-text-after"] = textarea.Text.substring(selectionEnd);
                    sessionStorage["originURL"] = window.location;
                    window.location = "diagrams.net/new/";
                }

                if (span.classList.contains('fa-lock'))
                {
                    textarea.unlocked = true;
                    span.classList.remove('fa-lock');
                    span.classList.add('fa-unlock');
                }
                else
                if (span.classList.contains('fa-unlock'))
                {
                    textarea.unlocked = false;
                    span.classList.remove('fa-unlock');
                    span.classList.add('fa-lock');
                }
            };
        });

        document.querySelectorAll('.app-window-body.edit textarea.dropzone').forEach((remarkupEditor) => {
            remarkupEditor.addEventListener('keydown', function(e) {
                if (e.ctrlKey  &&  !e.target.ctrlAction) {
                    switch (e.key) {
                        case 'b':
                            var btnBold = e.target.parentElement.querySelector('.app-edit-window-head > .phui-font-fa.fa-bold');
                            btnBold.click();
                            e.preventDefault();
                            e.target.ctrlAction = true;
                            return false;

                        case 'i':
                            var btnItalic = e.target.parentElement.querySelector('.app-edit-window-head > .phui-font-fa.fa-italic');
                            btnItalic.click();
                            e.preventDefault();
                            e.target.ctrlAction = true;
                            return false;
                    }
                }
            });

            remarkupEditor.addEventListener('keyup', function(e) {
                delete e.target.ctrlAction;
            });
        });
    }


    stopDecoding() {
        if (this.tmrDecodeRemarkup) {
            clearTimeout(this.tmrDecodeRemarkup);
        }
    }

    Cancel(button) {
        var csrf_token = document.querySelector('input[name=csrf_token]');
        if (csrf_token != null) csrf_token = csrf_token.value;

        var token = document.querySelector('input[name=token]').value;
        var textarea = document.querySelector('.app-window-body.edit > textarea');

        var referencedFiles = [ ...textarea.value.matchAll(/{F(-?[0-9]+)[^}]*}/g) ].map( function(m) { return m[1]; })
                                                                                   .join(',');

        var data = new FormData();
        data.append('token', token);
        data.append('referencedFiles', referencedFiles);
        data.append('csrf_token', csrf_token);

        var http = new XMLHttpRequest();
        http.open('POST', document.location.pathname + "?action=cancel", true);
        http.setRequestHeader('Content-type', 'multipart/form-data; charset=utf-8');
        http.onreadystatechange = function () {
            if (http.readyState == 4) {
                window.location = window.location.origin + window.location.pathname;
            };
        };

        http.send(data);
    }

    Decode(remarkup, dest) {
        this.stopDecoding();

        var xmlhttp = this.xmlhttp;
        xmlhttp.destinationField = dest;

        this.tmrDecodeRemarkup = setTimeout(function(){
            var data = new FormData();
            data.append('data', remarkup);
            data.append('url', document.URL);

            xmlhttp.open('POST', "remarkup/", true);
            xmlhttp.setRequestHeader('Content-type', 'multipart/form-data; charset=utf-8');
            xmlhttp.send(data);
        }, 300);
    }

    Save(button) {
        button.disabled = true;
        var form = button.parentElement;
        while (form != null && form.tagName !== 'FORM') form = form.parentElement;

        if (form != null) {
            var data = new FormData(form);
            var http = new XMLHttpRequest();
            http.open('POST', document.location.pathname + "?action=save", true);
            http.setRequestHeader('Content-type', 'multipart/form-data; charset=utf-8');
            http.onreadystatechange = function () {
                if (http.readyState == 4) {
                    if (!http.responseURL) {
                        window.location = window.location.origin + window.location.pathname;
                    } else {
                        window.location = http.responseURL.replace(/\?action=save.*/, "");
                    }
                };
           };

            http.send(data);
        }
    }
}

// ************************************************************************************************
class Responsiveness {
    constructor() {
        var me = this;

        window.onresize = function(event) {
            me.redraw();
        };

        this.redraw = function(ev) {
            if (window.matchMedia("(min-width: 1500px)").matches) {
                if (localStorage["phabrico-page-content-collapsed"] == "1") {
                    phabrico.appSideWindow.Collapse()
                } else {
                    phabrico.appSideWindow.Expand()
                }
            } else {
                phabrico.appSideWindow.Collapse()
            }
        };

        this.redraw();
    }
}

// ************************************************************************************************
class Search {
    constructor()
    {
        // initialize AJAX object
        const xmlhttp = new XMLHttpRequest();
        this.xmlhttp = xmlhttp;

        this.xmlhttp.onreadystatechange = function ()
        {
            if (xmlhttp.readyState == 4 && xmlhttp.status == 200) {
                popSearchResults.innerHTML = "";
                popSearchResults.classList.remove('show');

                var searchResults = JSON.parse(xmlhttp.responseText);
                for (var idx in searchResults)
                {
                    var result = searchResults[idx];

                    var a = document.createElement('a');
                    a.href = result.URL;
                    a.name = result.Description;
                    a.rel = result.Token;
                    a.className = "search-result";

                    var item = document.createElement('span');
                    a.appendChild(item);

                    var title = document.createElement('span');
                    title.className = "search-result-name";
                    title.innerText = result.Description;
                    item.appendChild(title);

                    var smallIcon = document.createElement('span');
                    item.appendChild(smallIcon);

                    var path = document.createElement('span');
                    path.className = "search-result-url";
                    item.appendChild(path);

                    if (result.URL.startsWith("maniphest/"))
                    {
                        smallIcon.className = "phui-font-fa fa-anchor lightgraytext";
                        path.innerText = "Maniphest Task";
                    }
                    else
                    if (result.URL.startsWith("phame/"))
                    {
                        smallIcon.className = "phui-font-fa fa-feed lightgraytext";
                        path.innerText = "Blog Post";
                    }
                    else
                    {
                        smallIcon.className = "phui-font-fa fa-book lightgraytext";
                        path.innerText = result.Path;
                    }

                    popSearchResults.appendChild(a);

                    popSearchResults.classList.add('show');
                }
            }
        }
    }

    Show(keyword) {
        if (event.key == "Tab" ||
            event.key == "Shift" ||
            event.key == "Control" ||
            event.key == "ArrowLeft" ||
            event.key == "ArrowRight" ||
            event.key == "ArrowUp" ||
            event.key == "ArrowDown" ||
            event.key == "ContextMenu")
        {
            // control character pressed -> skip ajax call
            return;
        }

        if (keyword == "")
        {
            // hide context menu when no input
            this.Hide();
        }

        if (event.key == "Enter")
        {
            if (popSearchResults.classList.contains('show') &&
                popSearchResults.querySelectorAll('a').length == 1)
            {
                // browse to the only selected item from the popup menu
                window.location = popSearchResults.querySelectorAll('a')[0].href;
            }

            return;
        }

        this.xmlhttp.open("GET", "search/" + keyword + "/", true);
        this.xmlhttp.send();
    }

    Hide() {
        popSearchResults.classList.remove('show');
    }
}

// ************************************************************************************************
class Synchronization {
    cancel()
    {
       dlgRequestSynchronize.style.display = 'none';
    }

    confirm() {
       var httpRequestNumberLocalUnfrozenChanges = new XMLHttpRequest();
       httpRequestNumberLocalUnfrozenChanges.overrideMimeType("application/json");
       httpRequestNumberLocalUnfrozenChanges.open('GET', document.baseURI + 'synchronize/prepare/', true);
       httpRequestNumberLocalUnfrozenChanges.onload  = function() {
           var jsonResponse = JSON.parse(httpRequestNumberLocalUnfrozenChanges.responseText);
           if (jsonResponse.NumberOfUnfrozenChanges == 0)
           {
              dlgRequestSynchronizeDetail.style.display = 'none';
           }
           else
           {
              dlgRequestSynchronizeDetail.style.display = 'block';
              dlgRequestSynchronizeDetail.innerHTML = phabrico.synchronization.htmlFormatString( Locale.Translate("There is [b]1 local modification[/b] ready to be uploaded the Phabricator server."),
                                                                                        ["[b]", "[/b]"]
                                                                                      )
                                                            .replace(/\[b\]/, '<b>')
                                                            .replace(/\[\/b\]/, '</b>');
              if (jsonResponse.NumberOfUnfrozenChanges > 1)
              {
                 // plural: there are more than 1 change
                dlgRequestSynchronizeDetail.innerHTML = phabrico.synchronization.htmlFormatString( Locale.Translate("There are [b]@@NBR-CHANGES@@ local modifications[/b] ready to be uploaded the Phabricator server.")
                                                                                                .replace(/@@NBR-CHANGES@@/, jsonResponse.NumberOfUnfrozenChanges),
                                                                                          ["[b]", "[/b]"]
                                                                                        )
                                                              .replace(/\[b\]/, '<b>')
                                                              .replace(/\[\/b\]/, '</b>');
              }
           }

           dlgRequestSynchronize.style.display = 'block';
       };
       httpRequestNumberLocalUnfrozenChanges.send(null);
    }

    htmlFormatString(str, exclude) {
        var result = "";
        var index = 0;

        while (index < str.length) {
            var excludeFound = false;
            for (var exc in exclude) {
                if (str.substring(index).startsWith(exclude[exc])) {
                    index += exclude[exc].length;
                    result += exclude[exc];
                    excludeFound = true;
                    break;
                }
            }

            if (excludeFound == false) {
                result += "&#" + str[index].charCodeAt(0) + ";"
                index++;
            }
        }

        return result;
    }

    start(frm,type) {
        phabrico.autoLogOff.disable();
        postForm(frm, "synchronize/" + type + "/");

        btnCloseSyncErrorDialog.style.display = 'none';
        dlgRequestSynchronize.style.display = 'none';
        requestStackTrace.innerText = "";
        dlgSynchronizing.style.display = 'block';
        phabrico.progressBar.show(syncProgress, "synchronize/status/");

        var tmrSynchronizing = setInterval(function(e) {
            if (phabrico.progressBar.currentValue == 100) {
                clearInterval(tmrSynchronizing);

                if (phabrico.progressBar.status == "MERGE-CONFLICT") {
                    phabrico.progressBar.text = Locale.Translate('Some changes could not be uploaded because newer versions of these documents or tasks were available on the Phabricator server. Verify them via \'Offline Changes\'');
                    btnCloseSyncErrorDialog.style.display = 'block';
                } else
                if (phabrico.progressBar.status == "ERROR") {
                    btnCloseSyncErrorDialog.style.display = 'block';
                    requestStackTrace.innerText = phabrico.progressBar.stackTrace;
                } else {
                    setTimeout(function() {
                        dlgSynchronizing.style.display = 'none';
                        phabrico.progressBar.currentValue = 0;

                        phabrico.autoLogOff.enable();
                        if (type == 'light') {
                            window.location = "configure/";
                        } else {
                            window.location.reload();
                        }
                    }, 2000);
                }
            }
        }, 1000);

        return false;
    }

    stop() {
        dlgSynchronizing.style.display = 'none';
        phabrico.progressBar.currentValue = 0;

        phabrico.autoLogOff.enable();

        if (phabrico.progressBar.status == "MERGE-CONFLICT") {
            window.location = "offline/changes/";
        } else {
            window.location.reload();
        }
    }
}

// ************************************************************************************************
class Table {
    constructor() {
        this.conceal = function(htmlObject) {
            htmlObject.querySelectorAll('td.concealed').forEach(function(td) {
                td.dataset.content = td.innerText;
                td.innerText = '********';

                td.onmouseenter = function(e) {
                    if (e.target.tmrHide !== 'undefined') {
                    clearTimeout(e.target.tmrHide);
                    }

                    if (e.target.innerText != e.target.dataset.content) {
                    e.target.innerText = e.target.dataset.content;
                    }
                }

                td.onmouseout = function(e) {
                    e.target.tmrHide = setTimeout(function(ee) {
                    e.target.innerText = '********';
                    }, 3000);
                }
            });
        };
    }
}

// ************************************************************************************************
class TextAreaContextMenu {
    constructor(textArea, contextMenuTriggerCharacter, objectType, urlData, propertyResult, propertyInternalName, propertyReadableName = null, conditionToShow = null)
    {
        var me = this;
        me.showDropDownMenu = false;
        me.offsetSearchValue = 0;
        me.endOffsetSearchValue = 0;
        me.autocomplete = null;
        me.searchValue = null;
        me.contextMenuTriggerCharacter = contextMenuTriggerCharacter;
        me.urlData = urlData;
        me.ObjectType = objectType;
        me.PropertyResult = propertyResult;
        me.PropertyInternalName = propertyInternalName;
        me.PropertyReadableName = propertyReadableName
        me.conditionToShow = conditionToShow;

        // credits for getCaretCoordinates function go to Dan Dascalescu
        me.getCaretCoordinates = function(element, position) {
            var isFirefox = !(window.mozInnerScreenX == null);
            var computed, style;

            // The properties that we copy into a hidden div.
            // Note that some browsers, such as Firefox,
            // do not concatenate properties, i.e. padding-top, bottom etc. -> padding,
            // so we have to do every single property specifically.
            var properties = [
                'boxSizing',
                'width',  // on Chrome and IE, exclude the scrollbar, so the hidden div wraps exactly as the textarea does
                'height',
                'overflowX',
                'overflowY',  // copy the scrollbar for IE

                'borderTopWidth',
                'borderRightWidth',
                'borderBottomWidth',
                'borderLeftWidth',

                'paddingTop',
                'paddingRight',
                'paddingBottom',
                'paddingLeft',

                // https://developer.mozilla.org/en-US/docs/Web/CSS/font
                'fontStyle',
                'fontVariant',
                'fontWeight',
                'fontStretch',
                'fontSize',
                'lineHeight',
                'fontFamily',

                'textAlign',
                'textTransform',
                'textIndent',
                'textDecoration',  // might not make a difference, but better be safe

                'letterSpacing',
                'wordSpacing'
            ];

            var hiddenDiv = document.createElement('div');
            hiddenDiv.id = element.nodeName + '--hidden-div';
            document.body.appendChild(hiddenDiv);

            style = hiddenDiv.style;
            computed = getComputedStyle(element);

            // default textarea styles
            style.whiteSpace = 'pre-wrap';
            if (element.nodeName !== 'INPUT')
            style.wordWrap = 'break-word';  // only for textarea-s

            // position off-screen
            style.position = 'absolute';  // required to return coordinates properly
            style.top = element.offsetTop + parseInt(computed.borderTopWidth) + 'px';
            style.left = "400px";
            style.visibility = 'hidden';

            // transfer the element's properties to the div
            properties.forEach(function (prop) {
                style[prop] = computed[prop];
            });

            if (isFirefox) {
                style.width = parseInt(computed.width) - 2 + 'px'  // Firefox adds 2 pixels to the padding - https://bugzilla.mozilla.org/show_bug.cgi?id=753662

                // Firefox lies about the overflow property for textareas: https://bugzilla.mozilla.org/show_bug.cgi?id=984275
                if (element.scrollHeight > parseInt(computed.height))
                    style.overflowY = 'scroll';
            } else {
                style.overflow = 'hidden';  // for Chrome to not render a scrollbar; IE keeps overflowY = 'scroll'
            }

            hiddenDiv.textContent = element.value.substring(0, position);

            // the second special handling for input type="text" vs textarea: spaces need to be replaced with non-breaking spaces - http://stackoverflow.com/a/13402035/1269037
            if (element.nodeName === 'INPUT')
                hiddenDiv.textContent = hiddenDiv.textContent.replace(/\s/g, "\u00a0");

            var span = document.createElement('span');
            // Wrapping must be replicated *exactly*, including when a long word gets
            // onto the next line, with whitespace at the end of the line before (#7).
            // The  *only* reliable way to do that is to copy the *entire* rest of the
            // textarea's content into the <span> created at the caret position.
            // for inputs, just '.' would be enough, but why bother?
            span.textContent = element.value.substring(position) || '.';  // || because a completely empty faux span doesn't render at all
            span.style.backgroundColor = "lightgrey";
            hiddenDiv.appendChild(span);

            var coordinates = {
                top: span.offsetTop + parseInt(computed['borderTopWidth']),
                left: span.offsetLeft + parseInt(computed['borderLeftWidth'])
            };

            return coordinates;
        }

        me.remarkupInput = function(e) {
            var textarea = e.target;
            var keyPressed = "";
            var eventType = e.type;
            var eventKey = e.key;

            if (eventType == "keydown")
            {
                if (me.autocomplete == null)
                {
                    return;
                }

                if (eventKey == "Backspace")
                {
                    me.endOffsetSearchValue--;

                    if (me.endOffsetSearchValue < me.offsetSearchValue)
                    {
                        if (me.autocomplete != null)
                        {
                            document.body.removeChild(me.autocomplete);
                            me.autocomplete = null;
                        }
                        return;
                    }

                    if (textarea.selectionStart == textarea.selectionEnd)
                    {
                        me.searchValue = textarea.value.substring(me.offsetSearchValue, textarea.selectionStart - 1)
                                       + textarea.value.substring(textarea.selectionEnd - 1, me.endOffsetSearchValue);
                    }
                    else
                    {
                        me.searchValue = textarea.value.substring(me.offsetSearchValue, textarea.selectionStart)
                                       + textarea.value.substring(textarea.selectionEnd, me.endOffsetSearchValue + 1);
                    }
                }
                else
                if (eventKey == "Escape")
                {
                    document.body.removeChild(me.autocomplete);
                    me.autocomplete = null;
                    return;
                }
                else
                if (eventKey == "ArrowLeft")
                {
                    // hide contextmenu if cursor went before contextmenu-trigger character
                    if (me.offsetSearchValue >= textarea.selectionStart)
                    {
                        document.body.removeChild(me.autocomplete);
                        me.autocomplete = null;
                        return;
                    }
                }
                else
                if (eventKey == "ArrowRight")
                {
                    // hide contextmenu if cursor went after last entered contextmenu-character
                    if (me.endOffsetSearchValue <= textarea.selectionEnd)
                    {
                        document.body.removeChild(me.autocomplete);
                        me.autocomplete = null;
                        return;
                    }
                }
                else
                if (eventKey == "ArrowUp")
                {
                    var highlightedMenuItem = document.querySelector('.autocomplete-list a.menuitem.focused');
                    var selectedIndex = Array.prototype.indexOf.call(me.autocomplete.list.children, highlightedMenuItem);

                    selectedIndex = selectedIndex - 1;

                    if (selectedIndex < 0)
                    {
                        selectedIndex = me.autocomplete.list.values.length - 1;
                    }

                    if (highlightedMenuItem != null)
                    {
                        highlightedMenuItem.classList.remove('focused');
                    }

                    me.autocomplete.list.children[selectedIndex].classList.add('focused');
                    e.preventDefault();
                    return;
                }
                else
                if (eventKey == "ArrowDown")
                {
                    var highlightedMenuItem = document.querySelector('.autocomplete-list a.menuitem.focused');
                    var selectedIndex = Array.prototype.indexOf.call(me.autocomplete.list.children, highlightedMenuItem);

                    selectedIndex = selectedIndex + 1;

                    if (me.autocomplete.list.values.length <= selectedIndex)
                    {
                        selectedIndex = 0;
                    }

                    highlightedMenuItem.classList.remove('focused');
                    me.autocomplete.list.children[selectedIndex].classList.add('focused');
                    e.preventDefault();
                    return;
                }
                else
                if (eventKey == "Tab")
                {
                    // disable TAB keypress
                    e.preventDefault();

                    // convert TAB to ENTER (to close the context menu)
                    eventType = "keypress";
                    eventKey = "Enter";
                }
                else
                {
                    return;
                }
            }

            if (eventType == "keypress")
            {
                keyPressed = eventKey;
                if (me.showDropDownMenu == false && keyPressed == me.contextMenuTriggerCharacter)
                {
                    me.showDropDownMenu = true;

                    // contextmenu can only be shown if previous character allows it
                    var previousCharacter = textarea.value.substring(textarea.selectionStart - 1, textarea.selectionStart);
                    if ([' ','\n','\t','.','-',')','>','!','|',''].indexOf(previousCharacter) < 0)
                    {
                        me.showDropDownMenu = false;
                    }

                    if (me.showDropDownMenu)
                    {
                        me.searchValue = "";
                        me.offsetSearchValue = textarea.selectionStart + 1;
                        me.endOffsetSearchValue = me.offsetSearchValue;
                    }
                    return;
                }

                if (me.autocomplete != null)
                {
                    if (eventKey == "Enter")
                    {
                        var highlightedMenuItem = document.querySelector('.autocomplete-list a.menuitem.focused');
                        if (highlightedMenuItem != null)
                        {
                            highlightedMenuItem.click();
                        }
                        else
                        {
                            document.body.removeChild(me.autocomplete);
                            me.autocomplete = null;
                        }

                        e.preventDefault();
                        return;
                    }
                }

                // contextmenu should be hidden when the next character (=new key pressed) says so
                if (['.',' ', ':', ',', ')','#', '@', '!', '?', '{', '}'].indexOf(keyPressed) >= 0)
                {
                    if (me.autocomplete != null)
                    {
                        document.body.removeChild(me.autocomplete);
                        me.autocomplete = null;
                    }
                    return;
                }

                if (me.autocomplete == null)
                {
                    // check if trigger character was removed (e.g. by backspace)
                    if (textarea.selectionStart < 1 ||
                        textarea.value.substring(textarea.selectionStart - 1, textarea.selectionStart) != me.contextMenuTriggerCharacter)
                    {
                        // don't show context menu
                        me.showDropDownMenu = false;
                        return;
                    }

                    // check if trigger character is pressed twice
                    if (textarea.selectionStart >= 2 &&
                        textarea.value.substring(textarea.selectionStart - 2, textarea.selectionStart - 1) == me.contextMenuTriggerCharacter)
                    {
                        // don't show context menu
                        me.showDropDownMenu = false;
                        return;
                    }
                }

                me.endOffsetSearchValue++;

                if (me.autocomplete != null) {
                    me.searchValue = textarea.value.substring(me.offsetSearchValue, textarea.selectionStart)
                                    + keyPressed
                                    + textarea.value.substring(textarea.selectionEnd, me.endOffsetSearchValue);
                }
            }

            var searchText = me.searchValue;
            if (searchText == null || searchText.length == 0)
            {
                searchText = "Type a " + me.ObjectType + "name...";
            }

            if (me.showDropDownMenu || me.autocomplete != null)
            {
                if (me.autocomplete == null)
                {
                    me.searchValue += keyPressed;
                }

                var getContextMenuItems = new XMLHttpRequest();
                getContextMenuItems.overrideMimeType("application/json");
                getContextMenuItems.open('POST', me.urlData + me.searchValue + "/", true);
                getContextMenuItems.setRequestHeader('Content-type', 'multipart/form-data; charset=utf-8');
                getContextMenuItems.onload  = async function() {
                    var menuItems = null;
                    var contentIsHTML = null;
                    var fontAwesomeIcon = null;
                    try {
                        var contextMenuContent = JSON.parse(getContextMenuItems.responseText);
                        menuItems = contextMenuContent.records;
                        contentIsHTML = (typeof contextMenuContent.contentIsHTML == 'undefined') ? false : contextMenuContent.contentIsHTML;
                        fontAwesomeIcon = (typeof contextMenuContent.fontAwesomeIcon == 'undefined') ? null : contextMenuContent.fontAwesomeIcon;                        
                    } catch(exc) {
                        return;
                    }

                    if (me.conditionToShow != null) {
                        var filteredMenuItems = [];
                        var menuItemsIndex = 0;
                        while (menuItemsIndex < menuItems.length && filteredMenuItems.length < 5) {   // max 5 menuitems
                            var item = menuItems[menuItemsIndex];
                            if (me.conditionToShow(item)) {
                                filteredMenuItems.push(item);
                            }

                            menuItemsIndex++;
                        }

                        menuItems = filteredMenuItems;
                    }
                    else
                    if (menuItems.length > 5) {
                        menuItems = menuItems.slice(0, 5);  // max 5 menuitems
                    }

                    // calculate position of context menu
                    var menuItemHeight = 28;
                    var cursorPosition = me.getCaretCoordinates(textarea, textarea.selectionEnd);
                    var textAreaBoundaries = textarea.getBoundingClientRect();
                    var menuPostion = {
                        top: textarea.offsetTop + cursorPosition.top + menuItemHeight,
                        left: cursorPosition.left
                    };
                    if (menuPostion.top + (2 + menuItems.length) * menuItemHeight > textAreaBoundaries.bottom)
                    {
                        menuPostion.top = menuPostion.top - menuItemHeight * (2.5 + menuItems.length);
                    }

                    // create context menu if it does not exist yet
                    if (me.autocomplete == null)
                    {
                        me.showDropDownMenu = false;

                        me.autocomplete = document.createElement('div');
                        me.autocomplete.target = textarea;
                        me.autocomplete.classList.add('autocomplete');
                        me.autocomplete.style.left = menuPostion.left + 'px';

                        me.autocomplete.head = document.createElement('div');
                        me.autocomplete.head.classList.add('autocomplete-head');
                        me.autocomplete.appendChild(me.autocomplete.head);

                        me.autocomplete.head.prompt = document.createElement('span');
                        me.autocomplete.head.prompt.classList.add('autocomplete-prompt');
                        me.autocomplete.head.appendChild(me.autocomplete.head.prompt);

                        me.autocomplete.head.prompt.icon = document.createElement('span');
                        if (fontAwesomeIcon != null) {
                            me.autocomplete.head.prompt.icon.classList.add('icon');
                            me.autocomplete.head.prompt.icon.classList.add('phui-font-fa');
                            me.autocomplete.head.prompt.icon.classList.add(fontAwesomeIcon);
                            me.autocomplete.head.prompt.icon.classList.add('bluegrey');
                        }
                        me.autocomplete.head.prompt.appendChild(me.autocomplete.head.prompt.icon);

                        me.autocomplete.head.prompt.content = document.createTextNode('Find ' + me.ObjectType + ': ');
                        me.autocomplete.head.prompt.appendChild(me.autocomplete.head.prompt.content);

                        me.autocomplete.head.echo = document.createElement('span');
                        me.autocomplete.head.echo.classList.add('autocomplete-echo');
                        me.autocomplete.head.echo.innerText = getContextMenuItems.inputString;
                        me.autocomplete.head.appendChild(me.autocomplete.head.echo);

                        me.autocomplete.list = document.createElement('div');
                        me.autocomplete.list.classList.add('autocomplete-list');
                        me.autocomplete.appendChild(me.autocomplete.list);
                    }
                    else
                    {
                        // clear contextmenu content
                        me.autocomplete.list.innerHTML = "";
                    }

                    me.autocomplete.list.values = menuItems;
                    me.autocomplete.style.top = menuPostion.top + 'px';

                    // parse received JSON data and convert it to contextmenu content
                    me.autocomplete.list.values.forEach(function(item) {
                        var anchor = document.createElement('a');
                        me.autocomplete.list.appendChild(anchor);

                        anchor.href = document.location.pathname + "#";
                        if (me.PropertyReadableName == null) {
                            anchor.name = item[me.PropertyInternalName];
                        } else {
                            anchor.name = item[me.PropertyInternalName] + " (" + item[me.PropertyReadableName] + ")";
                        }
                        anchor.shortenedName = item[me.PropertyResult];
                        if (contentIsHTML) {
                            var dummyTextArea = document.createElement("textarea");
                            dummyTextArea.innerHTML = anchor.shortenedName;
                            anchor.shortenedName = dummyTextArea.value;
                        }
                        anchor.classList.add('menuitem');
                        anchor.onclick = function(e) {
                            var clickedAnchor = e.target.children[0].closest('a');
                            if (fontAwesomeIcon == null) {
                                me.autocomplete.target.setRangeText(clickedAnchor.shortenedName + " ", me.offsetSearchValue - 1, me.endOffsetSearchValue);
                            } else {
                                me.autocomplete.target.setRangeText(clickedAnchor.shortenedName + " ", me.offsetSearchValue, me.endOffsetSearchValue);
                            }
                            me.autocomplete.target.focus()
                            me.autocomplete.target.selectionStart += clickedAnchor.shortenedName.length + 1;

                            // trigger oninput event for refreshing remarkup decoding
                            var oninputEvent = new Event('input', { 'bubbles': true, 'cancelable': true });
                            me.autocomplete.target.dispatchEvent(oninputEvent);

                            document.body.removeChild(me.autocomplete);
                            me.autocomplete = null;
                            return false;
                        };

                        if (me.autocomplete.list.values[0] == item) {
                            anchor.classList.add('focused');
                        }

                        anchor.head = document.createElement('span');
                        anchor.appendChild(anchor.head);

                        anchor.head.icon = document.createElement('span');
                        anchor.head.icon.classList.add('icon');
                        anchor.head.icon.classList.add('phui-font-fa');
                        if (fontAwesomeIcon == null) {
                            anchor.head.icon.innerHTML = item[me.PropertyResult];
                        } else {
                            anchor.head.icon.classList.add('fa-user');
                        }
                        anchor.head.icon.classList.add('bluegrey');
                        anchor.head.appendChild(anchor.head.icon);

                        if (me.PropertyReadableName == null) {
                            anchor.head.content = document.createTextNode(item[me.PropertyInternalName]);
                        } else {
                            anchor.head.content = document.createTextNode(item[me.PropertyInternalName] + " (" + item[me.PropertyReadableName] + ")");
                        }
                        anchor.head.appendChild(anchor.head.content);
                    });

                    document.body.appendChild(me.autocomplete);
                }

                // get JSON data
                getContextMenuItems.inputString = me.searchValue;
                getContextMenuItems.send(null);

                if (me.autocomplete != null)
                {
                    me.autocomplete.head.echo.innerText = searchText;
                    me.showDropDownMenu = false;
                }
            }
        }

        // set event handlers
        textArea.addEventListener('keydown', me.remarkupInput);
        textArea.addEventListener('keypress', me.remarkupInput);
    }
}

// ************************************************************************************************
class TextAreaDropZone {
    constructor()
    {
        var me = this;

        this.fileReaderLoad = function(evt, files, i, isTranslation) {
                var chunkSize = 0x400000 - 0x1000;

                var getIDForNewFileData = new FormData();
                getIDForNewFileData.append('url', document.URL);

                var getIDForNewFile = new XMLHttpRequest();
                getIDForNewFile.currentFile = files[i];
                getIDForNewFile.target = evt.target;
                getIDForNewFile.overrideMimeType("application/json");
                getIDForNewFile.open('POST', "file/getIDForNewFile/", true);
                getIDForNewFile.onload  = async function(evt) {
                    var currentFile = evt.target.currentFile;
                    var jsonResponse = JSON.parse(getIDForNewFile.responseText);
                    var nbrCompleteSlices = Math.round(currentFile.size / chunkSize);
                    var nbrPartialSlices = ((nbrCompleteSlices * chunkSize) == currentFile.size) ? 0 : 1;
                    var nbrSlices = nbrCompleteSlices + nbrPartialSlices;
                    var imgMaximized = "";

                    if (getIDForNewFile.currentFile.type.startsWith("image/"))
                    {
                        // we got an image -> set default size to full-size
                        imgMaximized = ", size=full";
                    }

                    // show File object alias into Remarkup textarea
                    var newCursorPosition = evt.target.target.selectionStart
                                          + ("{F" +  jsonResponse.ID + imgMaximized + "}").length;
                    var content = evt.target.target.Text;
                    var newcontent = content.substring(0, evt.target.target.selectionStart) +
                                        "{F" + jsonResponse.ID + imgMaximized + "}" +
                                        content.substring(evt.target.target.selectionEnd);
                    evt.target.target.Text = newcontent;
                    evt.target.target.selectionStart = newCursorPosition;
                    evt.target.target.selectionEnd = newCursorPosition;

                    var autoLogOffWasEnabled = phabrico.autoLogOff.isEnabled;
                    if (autoLogOffWasEnabled) {
                        phabrico.autoLogOff.disable();
                    }

                    // upload file in chunks
                    var nbrChunksUploaded = 0;
                    evt.nbrHttpUploadCalls = 0;
                    for (var s = 0; s < nbrSlices; s++)
                    {
                        while (evt.nbrHttpUploadCalls > 0)
                        {
                            await sleep(50);
                        }

                        var blob = currentFile.slice(s * chunkSize, (s + 1) * chunkSize );
                        var uploadChunk = new XMLHttpRequest();

                        uploadChunk.onreadystatechange = function() {
                            if (edit.classList.contains('statusbar') == false)
                            {
                                editStatusBar.innerText = Locale.Translate("Uploading @@FILE@@  (@@PERCENTAGE@@ %)").replace(/@@FILE@@/, currentFile.name).replace(/@@PERCENTAGE@@/, "0");
                                edit.classList.add('statusbar');
                            }

                            if (uploadChunk.readyState == 4)
                            {
                                nbrChunksUploaded++;

                                var percentage = parseInt((nbrChunksUploaded * 100) / nbrSlices);
                                if (percentage > 100) percentage = 100;

                                editStatusBar.innerText = Locale.Translate("Uploading @@FILE@@  (@@PERCENTAGE@@ %)").replace(/@@FILE@@/, currentFile.name).replace(/@@PERCENTAGE@@/, percentage);

                                if (nbrChunksUploaded == nbrSlices)
                                {
                                    editStatusBar.innerText = Locale.Translate("@@FILE@@ uploaded...").replace(/@@FILE@@/, currentFile.name);

                                    if (typeof right !== "undefined"  &&  typeof remarkup !== "undefined") {
                                        remarkup.Decode(evt.target.target.value, right);
                                    }

                                    setTimeout(function() {
                                        if (autoLogOffWasEnabled) {
                                            phabrico.autoLogOff.enable();
                                        }

                                        edit.classList.remove("statusbar");
                                    }, 1500);
                                }

                                evt.nbrHttpUploadCalls--;
                            }
                        };

                        evt.nbrHttpUploadCalls++;
                        uploadChunk.open('POST', "file/uploadChunk/" + jsonResponse.ID + "/" + nbrSlices + "/" + (s + 1) + "/" + (isTranslation ? 1 : 0) + "/" + currentFile.name, true);
                        uploadChunk.setRequestHeader('Content-type', 'application/octet-stream');
                        uploadChunk.send(blob);
                    }
                };
                getIDForNewFile.send(getIDForNewFileData);
            }

        this.handleFileSelect = function(evt) {
            evt.stopPropagation();
            evt.preventDefault();

            var isTranslation = document.querySelector('.edit-container.translation') != null;

            var files = evt.dataTransfer.files; // FileList object.
            for (var i = 0; i < files.length; i++)
            {
                var fileReader = new FileReader();
                fileReader.onload = (me.fileReaderLoad)(evt, files, i, isTranslation);

                fileReader.readAsBinaryString(files[i],"UTF-8");
            }

            evt.target.classList.remove('dragging');
        }

        this.handleDragLeave = function(evt) {
            evt.target.classList.remove('dragging');
            evt.stopPropagation();
            evt.preventDefault();
        }

        this.handleDragOver = function(evt) {
            evt.target.classList.add('dragging');
            evt.stopPropagation();
            evt.preventDefault();
            evt.dataTransfer.dropEffect = 'copy'; // Explicitly show this is a copy.
        }

        this.handleCtrlV = function (evt) {
            var items = (event.clipboardData  || event.originalEvent.clipboardData).items;
            var image = null;
            for (var i = 0; i < items.length; i++) {
                if (items[i].type.indexOf("text") === 0) {
                    var clipboardHtmlData = event.clipboardData.getData('text/html');

                    if (clipboardHtmlData.indexOf("xmlns:o=\"urn:schemas-microsoft-com:office:office\"") >= 0 &&
                        clipboardHtmlData.indexOf("xmlns:x=\"urn:schemas-microsoft-com:office:excel\"") >= 0
                       ) {
                        // we got some data copy/pasted from Excel -> strip to Remarkup format
                        var remarkupTable = clipboardHtmlData.replace(/[\s\S]*<table[^>]*>/,                '<table>')         // remove data before <table> tag and remove all <table> attributes
                                                             .replace(/<!--[^>]*-->/g,                      '')                // remove all HTML comments
                                                             .replace(/<col[^>]*>/g,                        '')                // remove all <col> tags
                                                             .replace(/<tr[^>]*>/g,                         '<tr>')            // remove all attributes from <tr> tags
                                                             .replace(/<td[^>]+class=[^>]+>([^<]*)<\/td>/g, '<th>$1</th>')     // replace all td-tags with a class attribute into th-tags
                                                             .replace(/<td[^>]*>/g,                         '<td>')            // remove all attributes from <td> tags
                                                             .replace(/<\/?span[^>]*>/g,                    '')                // remove all inner span tags
                                                             .replace(/>[\s]*</g,                           '><')              // remove all whitespace between tags
                                                             .replace(/<\/table>.*/g,                       '</table>')        // remove all data after </table> tag
                                                             .replace(/<tr>/g,                              '\n  <tr>')        // indent rows
                                                             .replace(/<\/tr>/g,                            '\n  </tr>')       // indent row-ends
                                                             .replace(/<td>/g,                              '\n    <td>')      // indent table-cell
                                                             .replace(/<th>/g,                              '\n    <th>')      // indent header-cell
                                                             .replace(/<\/table>/g,                         '\n</table>');     // indent table-end

                        // fix some HTML entities
                        remarkupTable = remarkupTable.replace(/&lt;/g, '<');
                        remarkupTable = remarkupTable.replace(/&gt;/g, '>');
                        remarkupTable = remarkupTable.replace(/&quot;/g, '"');
                        remarkupTable = remarkupTable.replace(/&amp;/g, '&');

                        var textarea = event.target;
                        textarea.value = textarea.value.substr(0, textarea.selectionStart) + remarkupTable + textarea.value.substr(textarea.selectionEnd);
                        event.preventDefault();
                        remarkup.Decode(textarea.value, right);
                        return true;
                    }

                    // paste plain text
                    return true;
                }

                if (items[i].type.indexOf("image") === 0) {
                    // paste image
                    image = items[i].getAsFile();

                    var fileReader = new FileReader();
                    fileReader.onload = (me.fileReaderLoad)(evt, evt.clipboardData.files, 0);
                    fileReader.readAsBinaryString(evt.clipboardData.files[i],"UTF-8");
                    return true;
                }
            }

            evt.preventDefault();
            evt.cancelBubble = true;
            return false;
        }

        // Setup the dnd listeners.
        var handleDragLeave = this.handleDragLeave;
        var handleDragOver = this.handleDragOver;
        var handleFileSelect = this.handleFileSelect;
        var handleCtrlV = this.handleCtrlV;
        document.querySelectorAll('textarea.dropzone')
                .forEach(function(dropZone) {
                    dropZone.addEventListener('dragover', handleDragOver, false);
                    dropZone.addEventListener('dragleave', handleDragLeave, false);
                    dropZone.addEventListener('drop', handleFileSelect, false);
                    dropZone.addEventListener('paste', handleCtrlV, false);
                });
    }
}

// ************************************************************************************************
function correctAnchorHrefsForUseBaseHtmlTag() {
    document.querySelectorAll("a[href^='#']").forEach(function(anchor) {
        anchor.href = document.location.pathname + anchor.attributes["href"].value;
    });
    document.querySelectorAll("a[href^='?']").forEach(function(anchor) {
        anchor.href = document.location.pathname + anchor.attributes["href"].value;
    });
}

function correctHeaderHeights() {
    var remarkupContent = document.querySelector('.remarkupContent');
    if (remarkupContent == null || typeof(remarkupContent) === 'undefined') return;

    var headerTags = Array.prototype.slice.call( remarkupContent.querySelectorAll('h1,h2,h3,h4,h5,h6'), 0 );
    headerTags = headerTags.map(function(h) {
        return parseInt(h.tagName.substring(1));
    });

    if (headerTags.filter((v, i, a) => a.indexOf(v) === i).length > 1) {
        remarkupContent.classList.add("multilevel-headers");
    } else {
        remarkupContent.classList.remove("multilevel-headers");
    }
}

function cumulativeOffset(element) {
    var top = 0, left = 0;
    do {
        top += element.offsetTop  || 0;
        left += element.offsetLeft || 0;
        element = element.offsetParent;
    } while(element);

    return {
        top: top,
        left: left
    };
}

function diffDrawLocationPane() {
    if (typeof locationPane == "undefined") return;

    window.removeEventListener('resize', diffResizeLocationPane);
    window.addEventListener('resize', diffResizeLocationPane);

    var data = Array.prototype.slice.call( document.querySelectorAll('td.left'), 0 )
                   .map(function(td) {
                       if (td.classList.contains('equal'))
                           return "equal";
                       else  if (td.classList.contains('empty'))
                           return "empty";
                       else  if (td.classList.contains('replace'))
                           return "replace";
                       else  if (td.classList.contains('delete'))
                       return "delete";
                   });
    var colorId = [];
    var colorStart = [ 1 ];

    data.forEach( function(v) {
        if (colorId[colorId.length - 1] != v) {
            colorId.push(v);
            colorStart.push( colorStart[colorStart.length - 1] );
        }

        colorStart[colorStart.length - 1] = colorStart[colorStart.length - 1] + 1;
    });


    var colorLength = [];
    for (var i=1; i<colorStart.length; i++) {
        colorLength.push( 100 * (colorStart[i] - colorStart[i-1]) / (colorStart[colorStart.length - 1]-1) )
    }

    locationPane.innerText = "";
    locationPane.tabIndex = 0;
    locationPane.removeEventListener('focusin', diffRestoreFocusLocationPane);
    locationPane.addEventListener('focusin', diffRestoreFocusLocationPane, true);

    // check if location view should be drawn
    var nbrAvailableRows = (fileLeft.scrollHeight - 24) / 24;
    var nbrVisibleRows = (fileLeft.clientHeight - 24) / 24;
    if (nbrVisibleRows < nbrAvailableRows) {
        // draw location view
        var heightVisible = fileLeft.clientHeight * (nbrVisibleRows / nbrAvailableRows);
        var pctScrollPosition = fileLeft.scrollTop / fileLeft.scrollTopMax;

        var locationView = document.createElement('div');
        locationView.className = 'locationPaneViewer';
        locationPane.insertBefore(locationView, null);

        var locationViewCurrent = document.createElement('div');
        locationViewCurrent.style.width = '9px';
        locationViewCurrent.style.background = '#08f5';
        locationViewCurrent.style.display = 'block';
        locationViewCurrent.style.position = 'relative';
        locationViewCurrent.style.top = 'calc(' + (pctScrollPosition * 100) + '% - ' + pctScrollPosition + ' * ' + heightVisible + 'px)';
        locationViewCurrent.style.height = heightVisible + 'px';
        locationView.insertBefore(locationViewCurrent, null);
    }

    var top = 0;
    for (var i=0; i<colorLength.length; i++) {
       var height = 100;
       if (i < colorLength.length - 1) {
          height = colorLength[i+1]
       }

       var div = document.createElement('div');
       div.style.width = "5px";
       div.style.height = colorLength[i] + "%";
       div.style.display = "block";
       div.addEventListener('mousemove', function(evt) {
          if (evt.buttons == 1) {
              diffDrawLocationPanePosition(evt);
          }
       });
       div.addEventListener('mousedown', function(evt) {
          if (evt.buttons == 1) {
              diffDrawLocationPanePosition(evt);
          }
       });
       div.className = colorId[i];

       locationPane.insertBefore(div, null);

       top = top + colorLength[i];
    }
}

function diffDrawLocationPanePosition(evt) {
    var locationPaneTop = locationPane.getBoundingClientRect().y;
    var locationPaneHeight = locationPane.getBoundingClientRect().height;

    var percentage = (evt.clientY - locationPaneTop) / locationPaneHeight;
    var selectedLine = parseInt(fileLeft.querySelectorAll('tr').length * percentage);

    fileLeft.scrollTop = (selectedLine - 1) * 24;
}

function diffResizeLocationPane(evt) {
   diffDrawLocationPane();
}

function diffRestoreFocusLocationPane(evt) {
    fileLeft.focus();
}

function finishHyperlinks() {
    document.querySelectorAll('.email-link, .phone-link, .phriction-link').forEach((hyperlink) => {
        var contextMenu = document.body.querySelector('.context-menu');
        if (contextMenu != null) document.body.removeChild(contextMenu);

        // set events for context-menu for hyperlink in decoded Remarkup
        hyperlink.addEventListener('contextmenu', function (ev) {
            ev.preventDefault();

            document.body.querySelectorAll('.context-menu').forEach((c) => {
                document.body.removeChild(c);
            });

            var contextMenu = document.createElement('div');
            document.body.appendChild(contextMenu);
            contextMenu.className = 'context-menu';
            contextMenu.style.display = 'block';
            contextMenu.style.position = 'absolute';
            contextMenu.style.left = ev.pageX + "px";
            contextMenu.style.top = ev.pageY + "px";

            var menuItems = document.createElement('ul');
            menuItems.className = 'menu';

            // == start Copy-to-clipboard ========================================================================================
            var mnuCopy = document.createElement('li');
            mnuCopy.className = 'copy';
            var mnuCopyAnchor = document.createElement('a');
            var mnuCopyIcon = document.createElement('i');
            mnuCopyIcon.className = 'fa fa-copy';
            var mnuCopyContent = document.createTextNode("Copy");
            mnuCopyAnchor.appendChild(mnuCopyIcon);
            mnuCopyAnchor.appendChild(mnuCopyContent);
            mnuCopy.appendChild(mnuCopyAnchor);
            menuItems.appendChild(mnuCopy);

            contextMenu.appendChild(menuItems);

            var clipboard = new ClipboardJS('.context-menu .copy a');
            if (hyperlink.href.startsWith('mailto:')) {
                mnuCopyAnchor.setAttribute('data-clipboard-text', hyperlink.href.substr('mailto:'.length));
            } else {
                mnuCopyAnchor.setAttribute('data-clipboard-text', hyperlink.href);
            }
            mnuCopyAnchor.addEventListener('mouseup', function (e) {
                setTimeout(function () {
                    contextMenu.removeChild(menuItems);
                }, 100);
            });
            // == end Copy-to-clipboard ==========================================================================================

            return false;
        }, false);
    });
}

function finishMetaDataElements() {
    document.querySelectorAll('.metadata').forEach((metadata) => {
        var contextMenu = document.body.querySelector('.context-menu');
        if (contextMenu != null) document.body.removeChild(contextMenu);

        // set events for context-menu for metadata in decoded Remarkup
        metadata.addEventListener('contextmenu', function (ev) {
            ev.preventDefault();

            document.body.querySelectorAll('.context-menu').forEach((c) => {
                document.body.removeChild(c);
            });

            var contextMenu = document.createElement('div');
            document.body.appendChild(contextMenu);
            contextMenu.className = 'context-menu';
            contextMenu.style.display = 'block';
            contextMenu.style.position = 'absolute';
            contextMenu.style.left = ev.pageX + "px";
            contextMenu.style.top = ev.pageY + "px";

            var menuItems = document.createElement('ul');
            menuItems.className = 'menu';

            // == start Copy-to-clipboard ========================================================================================
            var mnuCopy = document.createElement('li');
            mnuCopy.className = 'copy';
            var mnuCopyAnchor = document.createElement('a');
            var mnuCopyIcon = document.createElement('i');
            mnuCopyIcon.className = 'fa fa-copy';
            var mnuCopyContent = document.createTextNode("Copy");
            mnuCopyAnchor.appendChild(mnuCopyIcon);
            mnuCopyAnchor.appendChild(mnuCopyContent);
            mnuCopy.appendChild(mnuCopyAnchor);
            menuItems.appendChild(mnuCopy);

            contextMenu.appendChild(menuItems);

            var clipboard = new ClipboardJS('.context-menu .copy a');
            mnuCopyAnchor.setAttribute('data-clipboard-text', metadata.innerText);
            mnuCopyAnchor.addEventListener('mouseup', function (e) {
                setTimeout(function () {
                    contextMenu.removeChild(menuItems);
                }, 100);
            });
            // == end Copy-to-clipboard ==========================================================================================

            return false;
        }, false);
    });
}

function getElementOverlappingElement(elementBelow) {
    const boundingRect = elementBelow.getBoundingClientRect()
    // adjust coordinates to get more accurate results
    const left = boundingRect.left + 1
    const right = boundingRect.right - 1
    const top = boundingRect.top + 1
    const bottom = boundingRect.bottom - 1

    var elementAbove = document.elementFromPoint(left, top);
    if(elementAbove !== elementBelow) return elementAbove;

    elementAbove = document.elementFromPoint(right, top);
    if(elementAbove !== elementBelow) return elementAbove;

    elementAbove = document.elementFromPoint(left, bottom);
    if(elementAbove !== elementBelow) return elementAbove;

    elementAbove = document.elementFromPoint(right, bottom);
    if(elementAbove !== elementBelow) return elementAbove;

    return null;
}

function initializeTab(tab) {
    Array.prototype.slice.call(tab.querySelectorAll('.tab-head'), 0)
            .map( function(tabHeader) {
                tabChanged( tabHeader.children[0] );
            }
    );
}

function isElementBehindAppSideWindow(elementBelow) {
    var elementAbove = getElementOverlappingElement(elementBelow);
    if (elementBelow === elementAbove) return false;

    return Array.prototype.slice.call(document.querySelectorAll('.app-side-window'), 0)
                          .filter(function(appSideWindow) {
                              return appSideWindow.contains(elementAbove);
                           })
                          .length >= 1;
}

function isValidPassword(pwd, error) {
    if (typeof error === "undefined") {
        error = {};
    }

    if (pwd.length < 12) {
        error.reason = Locale.Translate("Password should be at least 12 characters long");
        return false;
    }

    if (pwd.match("[^ \u{3040}-\u{30ff}\u{3400}-\u{4dbf}\u{4e00}-\u{9fff}\u{f900}-\u{faff}\u{ff66}-\u{ff9f}]") == null) {
        // all characters are chinese -> password is valid
        error.reason = '';
        return true;
    }

    if (pwd.match(/[A-Z]/) == null) {
        error.reason = Locale.Translate("Password should contain at least 1 capital letter");
        return false;
    }

    if (pwd.match(/[a-z]/) == null) {
        error.reason = Locale.Translate("Password should contain at least 1 lowercase letter");
        return false;
    }

    if (pwd.match(/[0-9]/) == null) {
        error.reason = Locale.Translate("Password should contain at least 1 number");
        return false;
    }

    if (pwd.match(/[!"#$%&'()*+,-./:;<=>?@\[\\\]^_`{|}~]/) == null) {
        error.reason = Locale.Translate("Password should contain at least 1 punctuation character");
        return false;
    }

    error.reason = '';
    return true;
}

function phrictionCorrectButtonLocations() {
    // when the window gets scrolled, the copy-button (or other type of edit button) might get hidden behind the right action menu in Phriction
    // if so, move the button a bit more to the left
    document.querySelectorAll('div.codeblock button.codeblock.copy, div.image-container > a.button').forEach((btn) => {
        if (isElementBehindAppSideWindow(btn)) {
            btn.classList.add('overlapped');
        } else  {
            btn.classList.remove('overlapped');
            if (isElementBehindAppSideWindow(btn)) {
                btn.classList.add('overlapped');
            }
        }
    });

    var main = document.querySelector('main');
    var phuiDocument = document.querySelector('.phui-document');
    if (main != null && phuiDocument != null) {
        var mainWidth = parseInt(getComputedStyle(main).width);
        document.querySelectorAll('div.image-container img').forEach((img) => {
            if (mainWidth - 260 > img.naturalWidth + parseInt(getComputedStyle(document.querySelector('.phui-document')).left)) {
                var imageContainer = img.closest('.image-container');
                imageContainer.classList.add('non-overlappable')
            }
        });
    }

    // make sure the image-locator is not larger than the image itself
    var imageLocators = Array.prototype.slice.call(document.querySelectorAll('.image-locator'), 0)
                             .map(function(imgLocator) { 
                                return { 
                                    locator: imgLocator,
                                    image: imgLocator.querySelector('img')
                                }
                            });
    var imageLocatorsToBeResized = imageLocators.filter(function(imgLocator) { 
        return parseInt(imgLocator.locator.style.width) > imgLocator.image.width;
    });
    imageLocatorsToBeResized.forEach(function(imgLocator) {
	     imgLocator.locator.style.width = imgLocator.image.width + "px";
    })
}

function postForm(form, url)
{
    var data = new FormData(form);
    var http = new XMLHttpRequest();
    http.open('POST', url, true);
    http.setRequestHeader('Content-type', 'multipart/form-data; charset=utf-8');
    http.onload = function () {
        if (http.status != 200) {
            phabrico.autoLogOff.doLogOff();
        }

        if (this.responseText.length > 0) {
            document.body.innerHTML = this.responseText;
        } else
        if (url.startsWith("auth/login")) {
            var errorAuthentication = document.body.querySelector('.phui-info-severity-error');
            if (errorAuthentication != null) {
                errorAuthentication.style.display = 'none';
            }
        }

        if (this.statusText != "NOK" && this.responseURL.indexOf('/exception/?data=') == -1) {
            // add redirect code in case postForm was called from /auth/login
            if (url.startsWith("auth/login")) {
                // when running as IIS HTTP module, the statusText is overwritten by OK by IIS -> check again by means of error message
                var errorAuthentication = document.body.querySelector('.phui-info-severity-error');
                if (errorAuthentication == null || errorAuthentication.style.display != 'flex') {
                    if (url == "auth/login") url = "auth/login/";

                    var refererURL = url.indexOf("?ReturnURL=");
                    if (refererURL > -1) {
                        var redirectURL = refererURL + "?ReturnURL=".length;
                        if (redirectURL == "poke") redirectURL = "";
                        url = "auth/login" + url.substring(redirectURL);
                    }

                    var temp = document.createElement('script');
                    temp.innerHTML = 'window.location.replace("' + (document.baseURI + url.substring("auth/login/".length)).replace(/\/\/$/, "/") + '");';

                    document.body.appendChild(temp);
                }
            }
        }
    };
    http.send(data);
}

function resizeImage(direction, img, size) {
    if (direction == "width") {
        if (img.style.maxWidth === "" || img.style.maxWidth === "100%") {
            img.style.maxWidth = size + "px";
        } else {
            img.style.maxWidth = "100%";
        }
    }

    if (direction == "height") {
        if (img.style.maxHeight === "" || img.style.maxHeight === "100%") {
            img.style.maxHeight = size + "px";
        } else {
            img.style.maxHeight = "100%";
        }
    }
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function tabChanged(tabButton) {
    var selectedTab = tabButton.parentNode.querySelector('.selected');
    if (selectedTab != null) {
        var tabContent = document.getElementById(selectedTab.dataset.tabPageId);
        tabContent.style.display = 'none';
        selectedTab.removeAttribute('tabindex');
        selectedTab.classList.remove('selected');
    }

    selectedTab = tabButton.dataset.tabPageId;
    selectedTab = document.getElementById(selectedTab);
    if (selectedTab != null) {
        tabButton.classList.add('selected');
        tabButton.setAttribute('tabindex', '-1');
        selectedTab.style.display = 'flex';
    }
}

function toHTML(text) {
   var div = document.createElement('div');
   div.innerText = text;
   return div.innerHTML;
}

// ************************************************************************************************
document.addEventListener('DOMContentLoaded', function() {
    document.body.inputTag = new InputTag();

    // finalize all app-windows with a collapsable tag (minimize/maximize buttons are added)
    document.body.querySelectorAll('.app-window.collapsable').forEach(function(appWindow) {
        var appWindowHead = appWindow.querySelector('.app-window-head');
        if (appWindowHead != null && appWindowHead.lastElementChild.classList.contains('app-window-minmax') == false) {
            var button = document.createElement('span');
            button.className = 'fa fa-window-minimize right app-window-minmax';
            button.onclick = function(e) {
                var btn = e.target;
                var wnd = btn.parentElement.parentElement;
                var content = wnd.querySelector('.app-window-body');

                if (btn.classList.contains('fa-window-minimize')) {
                    btn.classList.remove('fa-window-minimize');
                    btn.classList.add('fa-window-maximize');

                    wnd.classList.add('collapsed');
                    wnd.style.height = '18px';
                    content.style.display = 'none';
                } else {
                    btn.classList.add('fa-window-minimize');
                    btn.classList.remove('fa-window-maximize');

                    wnd.classList.remove('collapsed');
                    wnd.style.height = null;
                    content.style.display = null;
                };
            };

            appWindowHead.appendChild(button);
        }
    });

    // finalize all textarea elements: add a Text property which will trigger the oninput event when it's been modified by javascript
    document.body.querySelectorAll('textarea').forEach(function(textarea) {
        Object.defineProperty(textarea, 'Text', {
            get: function(){
                return this.value
            },
            set: function(newValue){
                this.value = newValue;
                this.setAttribute('value', newValue)

                // trigger oninput event
                var event = new Event('input', {
                    bubbles: true,
                    cancelable: true,
                });

                setTimeout(function () {
                    textarea.dispatchEvent(event);
                }, 300); // slow event a bit down for uploaded images (otherwise the images will not be visible immediately after a copy/paste upload action)
            }
        });
    });

    // finalize all textarea elements in phriction-edit and maniphest-edit: when the cursor in the textarea is repositioned on a new line, the resulting remarkup at the right should also be scrolled
    document.body.querySelectorAll('.edit textarea').forEach(function(textarea) {
        if (typeof(right) !== 'undefined') {
            ['focus', 'keyup', 'click'].forEach(function(eventName) {
                textarea.addEventListener(eventName, function(e) {
                    var leftLine = (textarea.value.substr(0, textarea.selectionStart).match(/\n/g)||[]).length;
                    var rightDataLineSpans = Array.prototype.slice.call( right.querySelectorAll('span[data-line]'), 0 );
                    var nextRightLineSpan = rightDataLineSpans.find(function(span) { return parseInt(span.dataset.line) > leftLine});
                    var firstRightLineSpan = rightDataLineSpans[ rightDataLineSpans.indexOf(nextRightLineSpan) - 1 ];
                    if (typeof firstRightLineSpan === 'undefined') return;

                    if (textarea.unlocked == true) return;  // do nothing

                    textarea.ignoreRepositioning = true;

                    if (cumulativeOffset(nextRightLineSpan).top - cumulativeOffset(firstRightLineSpan).top < right.clientHeight + 200) {
                        // first and next line are both visible in right pane and there is even more than 200px extra available
                        right.scrollTop = cumulativeOffset(firstRightLineSpan).top - 270;
                    }
                    else
                    if (cumulativeOffset(nextRightLineSpan).top - cumulativeOffset(firstRightLineSpan).top < right.clientHeight + 100) {
                        // first and next line are both visible in right pane and there is even more than 100px extra available
                        right.scrollTop = cumulativeOffset(firstRightLineSpan).top - 170;
                    }
                    else
                    if (cumulativeOffset(nextRightLineSpan).top - cumulativeOffset(firstRightLineSpan).top < right.clientHeight) {
                        // first and next line are both visible in right pane
                        right.scrollTop = cumulativeOffset(firstRightLineSpan).top - 70;
                    }
                    else {
                        // first and next line are not both visible in right pane
                        right.scrollTop = cumulativeOffset(firstRightLineSpan).top
                                        - right.clientHeight / 2
                                        + (cumulativeOffset(nextRightLineSpan).top - cumulativeOffset(firstRightLineSpan).top)
                                            * (leftLine - firstRightLineSpan.dataset.line)
                                            / (nextRightLineSpan.dataset.line - firstRightLineSpan.dataset.line);
                    }
                });
            });
        }
    });

    document.body.querySelectorAll('.edit-container #right').forEach(function(decodedRemarkup) {
        decodedRemarkup.addEventListener('scroll', function(e) {
            if (textarea.unlocked == true) return;  // do nothing

            if (textarea.ignoreRepositioning == true) {
                textarea.ignoreRepositioning = false;
                return;
            }

            var topLine = Array.prototype.slice.call(right.querySelectorAll('span[data-line]')).find(function (span) {
                return cumulativeOffset(span).top > right.scrollTop + 20;
            })

            if (typeof topLine != 'undefined') {
                var topLineNumber = topLine.dataset.line;
                if (textarea.previousTopLineNumber == topLineNumber) return;
                textarea.previousTopLineNumber = topLineNumber;

                var regexNewlines = /\n/g;
                var match = null;
                for (var i=0; i<topLineNumber; i++) match=regexNewlines.exec(textarea.value);
                if (match != null) {
                    var selectionIndex = match.index;
                    var lineCount = topLineNumber;
                    while (match != null) {
                        lineCount++;
                        match=regexNewlines.exec(textarea.value);
                    }

                    textarea.selectionStart = textarea.selectionEnd = selectionIndex;
                    textarea.scrollTop = (textarea.scrollHeight / lineCount) * topLineNumber;
                }
            }
        });
    });


    // finalize all integer-only input fields
    document.body.querySelectorAll('.input-integer-only').forEach(function(numericInputField) {
        numericInputField.addEventListener("keypress", function(e) {
            var ch = (e.which) ? e.which : event.keyCode
            if (ch != 8 && (ch < 48 || ch > 57)) {  // check for backspace or numeric input
                e.preventDefault();
            }
        });

        numericInputField.addEventListener("blur", function(e) {
            if (e.target.value == "") {
                e.target.value = "0";
            }
        });

        numericInputField.type = "number";
        numericInputField.step = 1;
    });

    // finalize all concealed table headers
    var table = new Table();
    table.conceal( document.body );

    // finalize all transaction comboboxes (the ones where you can create comments, assign a task to someone, etc.)
    document.body.querySelectorAll('.app-window-head .select select.transaction').forEach(function(combobox) {
        combobox.onchange = function (e) {
            if (e.target.value != '+') {
                var header = document.querySelector('.select select:not(.language)')
                                     .closest('.app-main-window')
                                     .querySelector('.app-window-body.edit');

                var transaction = document.createElement('div');
                var label = document.createElement('label');
                var close = document.createElement('span');

                transaction.id = 'transaction-' + e.target.value;

                transaction.className = "transaction-item";
                label.className = "label";
                close.className = "close phui-font-fa fa-times-circle";

                transaction.appendChild(label);

                header.parentElement.insertBefore(transaction, header);

                close.onclick = function(e) {
                    var btn = e.target;
                    var tran = btn.closest('.transaction-item');
                    combobox.classList.remove(tran.id);
                    tran.parentElement.removeChild(tran);
                };

                combobox.classList.add('transaction-' + e.target.value);

                switch (e.target.value)
                {
                    case 'owner':
                        label.innerText = Locale.Translate('Assign / Claim');
                        var inputField = document.createElement('input');
                        inputField.name = 'owner';
                        inputField.dataset.url="user";
                        inputField.dataset.limit="1";
                        inputField.classList.add('input-tag');
                        transaction.appendChild(inputField);
                        transaction.insertBefore(inputField, null);
                        var inputTag = document.body.inputTag.create(inputField);
                        if (taskInputTagValues["owner"].Name != Locale.Translate("No users assigned")) {
                            document.body.inputTag.addTag(inputTag, taskInputTagValues["owner"].Name, taskInputTagValues["owner"].Token, "fa-user");
                        }
                        inputTag.inputText.focus();
                        break;

                    case 'priority':
                        label.innerText = Locale.Translate('Change Priority');
                        label.style.position = "relative";
                        var selectLabel = header.parentElement.querySelector('#priority');
                        var tempDiv = document.createElement('div');
                        var filler = document.createElement('span');
                        tempDiv.innerHTML = selectLabel.outerHTML;
                        tempDiv.querySelector("#priority").removeAttribute('id');
                        selectLabel = tempDiv.children[0];
                        selectLabel.style.display = 'inline-block';
                        selectLabel.children[0].name = 'priority';
                        filler.style.width = "100%";
                        close.style.marginRight = "0px";
                        transaction.appendChild(selectLabel);
                        transaction.appendChild(filler);
                        break;

                    case 'projectPHIDs':
                        label.innerText = Locale.Translate('Change Project Tags');
                        var inputField = document.createElement('input');
                        inputField.name = 'project';
                        inputField.dataset.url="tag";
                        inputField.classList.add('input-tag');
                        transaction.appendChild(inputField);
                        transaction.insertBefore(inputField, null);
                        var inputTag = document.body.inputTag.create(inputField);
                        taskInputTagValues["projectPHIDs"].forEach(
                            function(item) {
                                document.body.inputTag.addTag(inputTag, item.Name, item.Token, "fa-briefcase");
                            });
                        inputTag.inputText.focus();
                        break;

                    case 'status':
                        label.innerText = Locale.Translate('Change Status');
                        label.style.position = "relative";
                        var selectLabel = header.parentElement.querySelector('#status');
                        var tempDiv = document.createElement('div');
                        var filler = document.createElement('span');
                        tempDiv.innerHTML = selectLabel.outerHTML;
                        tempDiv.querySelector("#status").removeAttribute('id');
                        selectLabel = tempDiv.children[0];
                        selectLabel.style.display = 'inline-block';
                        selectLabel.children[0].name = 'status';
                        filler.style.width = "100%";
                        close.style.marginRight = "0px";
                        transaction.appendChild(selectLabel);
                        transaction.appendChild(filler);
                        break;

                    case 'subscriberPHIDs':
                        label.innerText = Locale.Translate('Change Subscribers');
                        var inputField = document.createElement('input');
                        inputField.name = 'subscriber';
                        inputField.dataset.url="subscriber";
                        inputField.classList.add('input-tag');
                        transaction.appendChild(inputField);
                        transaction.insertBefore(inputField, null);
                        var inputTag = document.body.inputTag.create(inputField);
                        taskInputTagValues["subscriberPHIDs"].forEach(
                            function(item) {
                                document.body.inputTag.addTag(inputTag, item.Name, item.Token, item.Icon);
                            });
                        inputTag.inputText.focus();
                        break;
                }

                transaction.appendChild(close);

                // align width of all transaction item labels
                var visibleLabels = Array.prototype.slice.call( document.querySelectorAll('.transaction-item label.label') );
                visibleLabels.forEach(function(transactionItemLabel) {
                    transactionItemLabel.style.minWidth = null;
                });
                var maxLabelWidth = Math.max.apply(Math, visibleLabels.map(function(transactionItemLabel) {
                    return parseInt(
                        getComputedStyle( transactionItemLabel ).width
                    );
                }) );
                visibleLabels.forEach(function(transactionItemLabel) {
                    transactionItemLabel.style.minWidth = maxLabelWidth + "px";
                });


                e.target.value = '+';
            }
        };
    });

    // finalize all remarkup table cells
    document.querySelectorAll('table.remarkup-table td')
            .forEach(function(td) {
                // when doubleclick on table cell -> select all text in cell
                td.ondblclick = function(e) {
                    var selection = window.getSelection();
                    var range = document.createRange();
                    range.selectNodeContents(e.target);
                    selection.removeAllRanges();
                    selection.addRange(range);
                };
            });

    // finalize all copy buttons in code blocks
    document.querySelectorAll('pre')
            .forEach(function(pre) {
                // when hovering over codeblock -> show copy button
                pre.onmouseenter = function(e) {
                    var pre = e.target;
                    var btn = pre.querySelector('button.codeblock.copy');
                    if (btn == null) return;

                    btn.style.opacity = 1;
                };

                // when mouse moving away from over codeblock -> hide copy button
                pre.onmouseleave = function(e) {
                    var pre = e.target;
                    var btn = pre.querySelector('button.codeblock.copy');
                    if (btn == null) return;

                    btn.style.opacity = 0;
                };
            });
    document.querySelectorAll('div.codeblock button.codeblock.copy')
            .forEach(function(btn) {
                // when click on button -> copy all text from code block to clipboard
                btn.onclick = function(e) {
                    var pre = e.target.parentNode.querySelector('pre');
                    var code = pre.querySelector('code');

                    var elem = document.createElement('textarea');
                    elem.style.opacity = 0;
                    elem.value = code.innerText;
                    document.body.appendChild(elem);

                    elem.select();
                    elem.setSelectionRange(0, 999999);
                    document.execCommand('copy');

                    document.body.removeChild(elem);
                };
            });
    window.addEventListener('scroll', function() {
        if (typeof phabrico.tmrWindowScrollEvent != 'undefined')
        {
            clearTimeout(phabrico.tmrWindowScrollEvent);
        }

        phabrico.tmrWindowScrollEvent = setTimeout(function() {
            phrictionCorrectButtonLocations();
        }, 100);
    });

    // finalize syntax highlighting codeblocks
    document.querySelectorAll('.codeblock pre code').forEach((codeblock) => {
        hljs.highlightBlock(codeblock);
    });

    // finalize synchronized scrolling in diff windows (e.g. stagediff, synchronizationdiff)
    document.querySelectorAll('.diff').forEach((diff) => {
        diff.scrollTimeout = null;
    });

    document.querySelectorAll('.diff #fileLeft, .diff #fileRight').forEach((diffContent) => {
        diffContent.addEventListener('scroll', function(e) {
            var scrollTimeout = document.querySelector('.diff').scrollTimeout;
            if (scrollTimeout != null) clearTimeout(scrollTimeout);

            var thisDiffView = e.target, otherDiffView = null;
            if (thisDiffView.id == 'fileLeft') {
               otherDiffView = document.querySelector('#fileRight');
            } else  {
               otherDiffView = document.querySelector('#fileLeft');
            }

            if (otherDiffView.scrollLeft != thisDiffView.scrollLeft  ||  otherDiffView.scrollTop != thisDiffView.scrollTop) {
                scrollTimeout = setTimeout(function () {
                    otherDiffView.scrollLeft = thisDiffView.scrollLeft;
                    otherDiffView.scrollTop = thisDiffView.scrollTop;
                    diffDrawLocationPane();
                }, 1);

                document.querySelector('.diff').scrollTimeout = scrollTimeout;
            }
        });

        diffContent.tabIndex = "0";
        diffContent.focus();
        diffContent.addEventListener('keydown', function(e) {
            if (e.key == "Home") {
                e.target.scrollTop = 0;
                e.stopPropagation();
                e.preventDefault();
                return;
            }

            if (e.key == "End") {
                e.target.scrollTop = 9999999;
                e.stopPropagation();
                e.preventDefault();
                return;
            }

            if (e.key == "ArrowDown") {
                e.target.scrollTop = e.target.scrollTop + 24;
                e.stopPropagation();
                e.preventDefault();
                return;
            }

            if (e.key == "ArrowUp") {
                e.target.scrollTop = e.target.scrollTop - 24;
                e.stopPropagation();
                e.preventDefault();
                return;
            }

            if (e.key == "PageDown") {
                var pageSize = fileLeft.clientHeight - 24;
                e.target.scrollTop = e.target.scrollTop + pageSize;
                e.stopPropagation();
                e.preventDefault();
                return;
            }

            if (e.key == "PageUp") {
                var pageSize = fileLeft.clientHeight - 24;
                e.target.scrollTop = e.target.scrollTop - pageSize;
                e.stopPropagation();
                e.preventDefault();
                return;
            }
        });
    });

    diffDrawLocationPane();


    // finalize phabrico const object
    Object.defineProperty( phabrico, "acceleratorKeys", {
        value: new AcceleratorKeys(),
        writable: false,
        enumerable: true,
        configurable: false
    });

    Object.defineProperty( phabrico, "appSideWindow", {
        value: new AppSideWindow(),
        writable: false,
        enumerable: true,
        configurable: false
    });

    Object.defineProperty( phabrico, "progressBar", {
        value: new ProgressBar(),
        writable: false,
        enumerable: true,
        configurable: false
    });

    Object.defineProperty( phabrico, "remarkup", {
        value: new Remarkup(),
        writable: false,
        enumerable: true,
        configurable: false
    });

    Object.defineProperty( phabrico, "responsiveness", {
        value: new Responsiveness(),
        writable: false,
        enumerable: true,
        configurable: false
    });

    Object.defineProperty( phabrico, "search", {
        value: new Search(),
        writable: false,
        enumerable: true,
        configurable: false
    });

    Object.defineProperty( phabrico, "synchronization", {
        value: new Synchronization(),
        writable: false,
        enumerable: true,
        configurable: false
    });

    Object.defineProperty( phabrico, "textAreaDropZone", {
        value: new TextAreaDropZone(),
        writable: false,
        enumerable: true,
        configurable: false
    });
}, false);

function htmlLoaded() {
    // make sure all buttons in Phriction are visible when needed (e.g. codeblock buttons, diagram buttons)
    phrictionCorrectButtonLocations();

    // convert href's which start with '?' to full relative paths
    // we are using the HTML BASE tag and '?' and '#' links are not working as one would expect
    // (they are pointing to the webserver's RootPath)
    correctAnchorHrefsForUseBaseHtmlTag();

    // fix size of H1 headers in case we also have subheaders (e.g. H2, H3, ...)
    correctHeaderHeights();

    // add some extra code to hyperlinks
    finishHyperlinks();

    // add some extra code to meta-elements (e.g. ip addresses in content)
    finishMetaDataElements();
}

window.addEventListener('load', function() {
    htmlLoaded();
}, false);


// set events for visualizing/hiding keyboard focus
window.addEventListener('keydown', function(e) {
    if (e.code == "Tab") {
        document.querySelectorAll('.phabrico-page-content').forEach((content) => {
            content.classList.add('tabkey');
        });
    }
}, false);

window.addEventListener('mousedown', function(e) {
    document.querySelectorAll('.phabrico-page-content').forEach((content) => {
        content.classList.remove('tabkey');
    });

    if (e.target.closest('.context-menu') == null) {
        document.body.querySelectorAll('.context-menu').forEach((contextmenu) => {
            document.body.removeChild(contextmenu);
        });
    }
}, false);

// disable contextmenu (except for images)
document.addEventListener('contextmenu', function (ev) {
    if (ev.target.tagName == 'IMG') return true; 

    ev.preventDefault();
    return false;
})

// correct colors for animated wait icon in case we're in dark mode
if (document.body.dataset.theme == "dark") {
    document.querySelectorAll('.wait svg rect').forEach(function (r) {
        r.setAttribute('fill', "#fff");
    });
}