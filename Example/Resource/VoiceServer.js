var oldCamera = null;

API.onServerEventTrigger.connect(function (eventName, args) {
    if (eventName === 'voiceInit') {
        initialize();
    }
});

function initialize() {
    oldCamera = API.getGameplayCamDir();
    API.displaySubtitle("Initialized!", 1000);
    API.startCoroutine(sendData);
}

function* sendData() {
    while (true) {
        yield 250;
        var cam = API.getGameplayCamDir();
        if (cam !== oldCamera) { // I don't think this is working
            API.triggerServerEvent("voiceCam", cam);
            oldCamera = cam;
        }
    }
}