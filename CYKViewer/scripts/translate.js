if (window.location.hostname.startsWith("shinycolors.enza.fun"))
{
    function Debug_Console2(message)
    {
        window.chrome.webview.postMessage(message);
    }

    Debug_Console2("Hi from translate function!");
    let divNode = document.createElement("div");
    divNode.style = "visibility: hidden;";
    divNode.id = "google_translate_element";

    let initScriptNode = document.createElement("script");
    initScriptNode.text =
    `
    function googleTranslateElementInit() {
        new google.translate.TranslateElement({pageLanguage: 'ja'}, 'google_translate_element');
    }
    `
    divNode.appendChild(initScriptNode);

    let styleNode = document.createElement("style")
    styleNode.textContent =
    `
    body {
        top: 0 !important;
    }

    iframe[id=':1.container'] {
        visibility: hidden !important;
    }
    `

    document.head.appendChild(styleNode);

    let scriptNode = document.createElement("script");
    scriptNode.type = "text/javascript";
    scriptNode.src = "//translate.google.com/translate_a/element.js?cb=googleTranslateElementInit";

    document.body.appendChild(divNode);
    document.head.appendChild(scriptNode);


    Debug_Console2("Translate finished");
    let tries = 0;
    function thing()
    {
        tries++;
        if (tries > 5)
        {
            return;
        }

        Debug_Console2(`Translate enable attempt ${tries}`);

        let sel = document.querySelector("select.goog-te-combo");
        if (!sel)
        {
            Debug_Console2("Failed to find combobox, try again");
            setTimeout(thing, 1000);
            return;
        }

        Debug_Console2(`sel has ${sel.childNodes.length} elements`);
        Debug_Console2(`Available languages: ${[...sel.childNodes].map(x => x.value).join(' ')}`);
        let option = [...sel.childNodes].find(x => x.value == "ko");
        Debug_Console2(`option is ${option}`);
        Debug_Console2(`option has index ${option?.index}`);
        let index = option?.index;
        if (index == null)
        {
            Debug_Console2("index was null, try again");
            setTimeout(thing, 1000);
            return;
        }
        Debug_Console2(`Index is ${index}`);
        sel.selectedIndex = index;
        sel.dispatchEvent(new Event("change"));

        Debug_Console2(`Translate enable complete`);
    }

    setTimeout(thing, 500);

    let translationEnabled = true;
    function ChangeTranslationStatus(enable)
    {
        let innerDoc = document.querySelector("iframe[id=':1.container']")?.contentDocument;
        if (!innerDoc)
        {
            Debug_Console2("Failed to find inner document");
            return false;
        }
        if (enable && !translationEnabled)
        {
            Debug_Console2("Re-enabling translation");
            innerDoc.querySelector("[id=':1.confirm']")?.click();
            translationEnabled = true;
        }
        else if (!enable && translationEnabled)
        {
            Debug_Console2("Disabiling translation");
            innerDoc.querySelector("[id=':1.restore']")?.click();
            translationEnabled = false;
        }

        return translationEnabled;
    }
}
