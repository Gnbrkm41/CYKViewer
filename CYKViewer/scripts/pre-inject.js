// pre-inject.js - Script that runs before the actual localisation patch script to provide our own implementation
// of the Tampermonkey functions (GM_registerMenuCommand, GM_unregisterMenuCommand) and to prepare communications
// between the actual WPF app and WebView2.
//
// Notes: `window.chrome.webview.hostObjects.{name}` are host objects exposed as proxies by WebView2.
// Those host objects are registered in the C# code before this script is run.
// Normally you'd use proxies that return promises, but in our case that doesn't really seem possible
// so we're using the synchronous version by using `chrome.webview.hostObjects.sync.{name}`.

// https://stackoverflow.com/questions/31231622/event-listener-for-web-notification
// Maybe we can hijack notifications here so we can get notifications?

// A function that POSTs a message to the C# side, which it'll interpret as debug logs from the script.
function Debug_Console(message) {
    window.chrome.webview.postMessage(message);
}

Debug_Console("Hello from WebView2!");

// Shortcuts to the host objects
var SC_CommsExtractionMenuEntry = chrome.webview.hostObjects.sync.SC_CommsExtractionMenuEntry;
var SC_BgmEnableMenuEntry = chrome.webview.hostObjects.sync.SC_BgmEnableMenuEntry;

// Storage object for the menu entries
var SC_MenuEntries = {};

// Copied from tampermonkey source, probably some sort of unique ID generation?
let randomId = '';
randomId += Math.floor(Math.random() * 06121983 + 1);
randomId += ((new Date()).getTime().toString()).substr(10, 7);

// A convenience function that gets called from C# to invoke menu handlers.
function Implementation_InvokeHandler(menuCmdId) {
    if (SC_MenuEntries[menuCmdId]) {
        let SC_MenuEntry = SC_MenuEntries[menuCmdId];
        Debug_Console(`Invoking "${SC_MenuEntry.commandName}" (ID ${menuCmdId})`);
        SC_MenuEntries[menuCmdId].commandFunc();
    }
}

// Our implementation of Tampermonkey's GM_registerMenuCommand function.
function Implementation_registerMenuCommand(commandName, commandFunc, accessKey) {
    // accessKey is for shortcuts - we don't need it, ignore

    Debug_Console(`Menu Registration Request for "${commandName}"`);
    var menuId = randomId + '#' + commandName;

    // No entries found, add a new one
    if (SC_MenuEntries[menuId]) {
        Debug_Console(`"${commandName}" - found in the list (ID ${menuId}), updating`);
    }
    else {
        Debug_Console(`"${commandName}" - Not found in the list, appending`);
    }

    SC_MenuEntries[menuId] = {
        commandName: commandName,
        commandFunc: commandFunc,
    };

    // Call the C# code to let it know the menu's being updated if they're BGM/Comms extraction related
    if (commandName.includes("BGM")) {
        Debug_Console(`"${commandName}" - is BGM related, calling the host`);
        SC_BgmEnableMenuEntry.Set(menuId, commandName);
        Debug_Console(`"${commandName}" - Host called`);
    }
    else if (commandName.includes("커뮤")) {
        Debug_Console(`"${commandName}" - is comms related, calling the host`);
        SC_CommsExtractionMenuEntry.Set(menuId, commandName);
        Debug_Console(`"${commandName}" - Host called`);
    }

    Debug_Console(`"${commandName}" - Added to ID ${menuId}`);
    return menuId;
}

// Our implementation of Tampermonkey's GM_unregisterMenuCommand function.
function Implementation_unregisterMenuCommand(menuCmdId) {
    Debug_Console(`Menu removal requested for ID ${menuCmdId}`);
    if (SC_MenuEntries[menuCmdId]) {
        let menuEntry = SC_MenuEntries[menuCmdId];
        Debug_Console(`ID ${menuCmdId} ("${menuEntry.commandName}") - Found in the list, removing`);
        delete SC_MenuEntries[menuCmdId];

        // Call the C# code to let it know the menu's being updated if they're BGM/Comms extraction related
        if (menuEntry.commandName.includes("BGM")) {
            Debug_Console(`"${menuEntry.commandName}" - is BGM related, calling the host`);
            SC_BgmEnableMenuEntry.Set(null, null);
            Debug_Console(`"${menuEntry.commandName}" - Host called`);
        }
        else if (menuEntry.commandName.includes("커뮤")) {
            Debug_Console(`"${menuEntry.commandName}" - is comms related, calling the host`);
            SC_CommsExtractionMenuEntry.Set(null, null);
            Debug_Console(`"${menuEntry.commandName}" - Host called`);
        }
    }
    else {
        Debug_Console(`Non-existent ID, ignoring the request`);
    }
    Debug_Console(`Menu removal complete`);
}

if (window.location.hostname.includes("shinycolors.enza.fun")) {
    Debug_Console(`Hostname is "${window.location.hostname}", Injecting the GM functions`);
    window.GM_registerMenuCommand = Implementation_registerMenuCommand;
    window.GM_unregisterMenuCommand = Implementation_unregisterMenuCommand;
}
else {
    Debug_Console(`Hostname is "${window.location.hostname}", not injecting the GM functions`);
    // Also going to reset the buttons, because those are useless when we're not in the shinycolors page.
    SC_BgmEnableMenuEntry.Set(null, null);
    SC_CommsExtractionMenuEntry.Set(null, null);
}

// Taken from the Edge WebView2 examples page - disables the context menu (right click)
// We're disabling it since it's undesirable to get context menu on touchscreens (which coincides with long clicks)
window.addEventListener('contextmenu', window => { window.preventDefault(); });
// Same as above - disables drag & drop (which is completely unnecessary for us)
window.addEventListener('dragover', function (e) { e.preventDefault(); }, false);
window.addEventListener('drop', function (e) { e.preventDefault(); }, false);
