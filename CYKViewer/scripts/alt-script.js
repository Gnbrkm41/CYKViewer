var bgmEnabled = localStorage.getItem("cyk:bgmEnabled");

window.addEventListener("blur", (function (e) {
    if (bgmEnabled === "true") {
        e.stopImmediatePropagation();
    }
}), false);

document.addEventListener("visibilitychange", (function (e) {
    if (bgmEnabled === "true") {
        e.stopImmediatePropagation();
    }
}));

var muteMenuId;
var muteMenuCallback = function () {
    if (bgmEnabled === "true") {
        bgmEnabled = "false";
        localStorage.removeItem("cyk:bgmEnabled");
        GM_unregisterMenuCommand(muteMenuId);
        muteMenuId = GM_registerMenuCommand("백그라운드 BGM 켜기", muteMenuCallback, null);
    }
    else {
        bgmEnabled = "true";
        localStorage.setItem("cyk:bgmEnabled", "true");
        GM_unregisterMenuCommand(muteMenuId);
        muteMenuId = GM_registerMenuCommand("백그라운드 BGM 끄기", muteMenuCallback, null);
    }
}

muteMenuId = window.GM_registerMenuCommand(bgmEnabled === "true" ? "백그라운드 BGM 끄기" : "백그라운드 BGM 켜기", muteMenuCallback);