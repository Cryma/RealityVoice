# RealityVoice [![Build status](https://ci.appveyor.com/api/projects/status/j13w2gt9rwfcfqxs/branch/master?svg=true)](https://ci.appveyor.com/project/Cryma/realityvoice/branch/master)

## [English readme](https://github.com/Cryma/RealityVoice/blob/master/README_EN.md)

RealityVoice ist ein Open-source 3D Voice-Chat für GTA 5 ([Grand Theft Multiplayer](https://gt-mp.net/)), basierend auf OpenAL und Lidgren.

**Wichtig:** Die Applikation ist noch nicht für den alltäglichen Gebrauch gedacht, es existieren noch viele Bugs und es ist sehr netzwerklastig. Das gute daran ist: Diese Dinge kann man beheben. Die Applikation richtet sich an Entwickler, die es unter Umständen in ihren eigenen Launcher einbinden wollen - nicht für den Endnutzer.

Es wäre trotzdem schön, wenn hier eine aktive Mitarbeit vorhanden ist, damit es schnellstmöglich eine stabile und *freie* Alternative für die derzeit bestehenden Voice-Chats gibt.

# Serverseitige Implementation
Um RealityVoice serverseitig zu nutzen, muss es einen Verweis zu VoiceChat.dll geben.

# TODO
  - [ ] Network-Messages reduzieren
		@root / 24.12.2017
		Rework angefangen. Netzwerk last verringert. 
  - [ ] ~~Push-to-talk~~ / Bessere Voice-Erkennung
  - [ ] Lautstärkeregler

# Genutze Libraries
  * [OpenAL.NET](https://github.com/DevJohnC/OpenAL.NET/) ([MIT](https://github.com/DevJohnC/OpenAL.NET/blob/master/mit);[LGPLv2](https://github.com/DevJohnC/OpenAL.NET/blob/master/lgpl))
  * [Lidgren](https://github.com/lidgren/lidgren-network-gen3/) ([MIT](https://github.com/lidgren/lidgren-network-gen3/blob/master/LICENSE))
