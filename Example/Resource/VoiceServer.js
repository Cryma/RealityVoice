var oldCamera = null;

API.onServerEventTrigger.connect(function (eventName, args) {
    if (eventName === 'voiceInit') {
        initialize();
    }
});

function initialize() {
    oldCamera = API.getGameplayCamDir();
    API.displaySubtitle("Initialized!");
    API.startCoroutine(sendData);
}

function* sendData() {
    while (true) {
        yield 250;
        var cam = API.getGameplayCamDir();
        if (oldCamera != cam) {
            API.triggerServerEvent("voiceCam", cam);
            oldCamera = cam;
        }
    }
}