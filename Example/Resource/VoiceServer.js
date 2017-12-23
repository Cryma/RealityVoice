API.onServerEventTrigger.connect(function (eventName, args) {
    if (eventName === 'voiceInit') {
        initialize();
    }
});

function initialize() {
    API.displaySubtitle("Initialized!");
    API.startCoroutine(sendData);
}

function* sendData() {
    while (true) {
        yield 250;
        API.triggerServerEvent("voiceCam", API.getGameplayCamDir());
    }
}