<div align="center">

<img src="logo.svg" alt="GobchatEx Logo" width="96" />

# GobchatEx

**Ein modernes FFXIV-Chat-Overlay für Rollenspieler.**

[![CI](https://github.com/Shuro/GobchatEx/actions/workflows/ci.yml/badge.svg)](https://github.com/Shuro/GobchatEx/actions/workflows/ci.yml)
[![Release](https://github.com/Shuro/GobchatEx/actions/workflows/release.yml/badge.svg)](https://github.com/Shuro/GobchatEx/actions/workflows/release.yml)
[![Latest release](https://img.shields.io/github/v/release/Shuro/GobchatEx?include_prereleases&label=latest)](https://github.com/Shuro/GobchatEx/releases/latest)
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL%20v3-blue.svg)](LICENSE.md)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%2F11-0078D6)

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/X6W621UWH7)

[English](README.md) · **Deutsch** · [Changelog](CHANGELOG.md)

</div>

> [!NOTE]
> **GobchatEx ist ein Community-Fork von [MarbleBag/Gobchat](https://github.com/MarbleBag/Gobchat)** (AGPL-3.0), modernisiert und gepflegt von Shuro.
> Die Umsetzung wurde inspiriert von [quisquous cactbot](https://github.com/quisquous/cactbot) und verwendet das ausgezeichnete [Sharlayan](https://github.com/FFXIVAPP/sharlayan)-Modul von FFXIVAPP, um den Speicher von FFXIV auszulesen.

## Was ist das?

GobchatEx ist ein Windows-Overlay, das über Final Fantasy XIV schwebt und Rollenspielern ein deutlich angenehmeres Chat-Erlebnis bietet - lesbare Farben, intelligente Hervorhebung, entfernungsabhängige Filterung und Gruppierung, alles über dem Spiel dargestellt, ohne im Weg zu sein.

Dieselbe Nachricht, einmal im Spiel und einmal durch GobchatEx:

| Im Spiel | Mit GobchatEx |
|:---:|:---:|
| ![Vanilla FFXIV-Chat](images/chat-ffxiv-vanilla.png) | ![GobchatEx-Chat](images/chat-gobchatex.png) |

## Funktionen

- **Rollenspiel-Formatierung** - automatische Farben für Sprache, Emotes und OOC-Kommentare, dazu einstellbare Tab-Stile und Chat-Dichte.- **Chat-Tabs** - erstelle beliebig viele Tabs und lege fest, welche Kanäle jeder zeigt und wie er formatiert wird.
- **Intelligente Hervorhebung (Erwähnungen)** - eine Wortliste, die ungeachtet der Groß-/Kleinschreibung immer hervorhebt, **plus Spieler-Erwähnungen pro Charakter**, die sich jeden Charakter merken, mit dem du dich anmeldest, und dessen Namen hervorheben. Optionale **unscharfe**, **Teilnamen-** und **Miqo'te**-Erkennung fangen Tippfehler und Namensteile ab.- **Reichweitenfilter** - blende Nachrichten je nach Entfernung des Sprechers aus oder lasse sie langsam ausblassen (live aus dem Spiel gemessen, in Yalm).
- **Gruppen** - sortiere Spieler in die sieben Spielgruppen *und* beliebig viele eigene; jede Gruppe ist benannt, umschaltbar und gestylt. Rechtsklicke einen Spieler im Overlay, um ihn direkt zu einer eigenen Gruppe hinzuzufügen oder daraus zu entfernen.
- **Einzelne Zeilen ausblenden** - rechtsklicke eine Zeile, um sie auszublenden; blende ausgeblendete Zeilen mit dem Knopf mit dem geschlossenen Auge wieder ein (nur für die aktuelle Sitzung).
- **Chat-Log** - schreibe deinen Chatverlauf optional in eine Datei mit anpassbarem Format.
- **Smart-Autoscroll** - scrolle hoch, um in Ruhe zu lesen; neue Nachrichten schieben die Ansicht nicht weiter. Scrolle zurück nach unten, um es wieder zu aktivieren.
- **Zieh- und größenverstellbar** - klicke den Pin-Knopf, um das Overlay zu entsperren, ziehe es an seiner Werkzeugleiste und verändere die Größe an jeder Kante; Position und Größe werden automatisch gespeichert.
- **Globale Tastenkürzel** - belege *Ein- & ausblenden* und *Suche fokussieren* unter *Einstellungen → App → Tastenkürzel*.

## Installation

Am einfachsten installierst du GobchatEx mit dem **Installer** - er richtet alles für dich ein und aktiviert die In-App-Updates.

1. Öffne die [neueste Version](https://github.com/Shuro/GobchatEx/releases/latest) und lade **`GobchatEx-win-Setup.exe`** herunter.
2. Führe sie aus. GobchatEx installiert sich pro Benutzer (ohne UAC-Abfrage) und startet anschließend.

> [!IMPORTANT]
> Die Oberfläche von GobchatEx wird mit der **Microsoft Edge WebView2 Runtime** (Evergreen) dargestellt, die auf aktuellen Windows 10/11 bereits vorhanden ist. Der Installer **richtet sie automatisch ein**, falls sie fehlt, und die .NET-Laufzeit ist eingebettet - mehr ist nicht nötig.

<details>
<summary>Lieber eine portable Version (ohne Installer)?</summary>

Lade stattdessen **`GobchatEx-win-Portable.zip`** aus der [neuesten Version](https://github.com/Shuro/GobchatEx/releases/latest) herunter:

1. Rechtsklick auf die Zip-Datei → **Eigenschaften** → unten rechts **Zulassen/Unblock** anhaken, dann **OK**.
2. Entpacke sie an einen beliebigen Ort (alles liegt bereits in einem `GobchatEx`-Ordner).
3. Öffne den Ordner und starte **GobchatEx.exe**.

Die portable Version richtet WebView2 nicht selbst ein. Startet GobchatEx also nicht, installiere die [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) selbst (auf den meisten aktuellen Windows 10/11 ist sie bereits vorhanden).

**Später von der portablen Version zum Installer wechseln?** Deine Einstellungen werden automatisch übernommen - beide Versionen legen deine Profile in `%AppData%\GobchatEx` ab, getrennt von den Programmdateien. Führe einfach `GobchatEx-win-Setup.exe` aus und lösche den alten portablen Ordner, sobald die installierte Version startet (sie bietet dir funktionierende Auto-Updates).

</details>

<details>
<summary>Erster Start &amp; Details</summary>

Beim allerersten Start zeigt GobchatEx vor der Oberfläche einen kurzen Einrichtungsbildschirm. Wähle deine **Sprache** und dein **Design**, lege fest, ob GobchatEx **automatisch nach Updates suchen** soll, und - falls eine vorhandene `%AppData%\Gobchat`-Installation des ursprünglichen Gobchat gefunden wird - ob deine **vorhandenen Profile übernommen** werden sollen (der alte Ordner wird kopiert und bleibt an seinem ursprünglichen Ort unangetastet). Deine Auswahl wirkt sofort und der Bildschirm erscheint danach nicht mehr; alles lässt sich später unter *Einstellungen → App* ändern.

GobchatEx stellt seine Oberfläche über die mit Windows ausgelieferte Microsoft Edge WebView2 Runtime dar, ein einmaliger Download einer Browser-Engine beim ersten Start entfällt also.

</details>

## Aktualisieren

Installierte Versionen aktualisieren sich selbst: GobchatEx prüft beim Start auf Updates, und die Patchnotes-Ansicht kann das Update für dich herunterladen und anwenden. (Bei der portablen Version lädst du einfach die neueste Version herunter und ersetzt deine Dateien.)

> [!TIP]
> Du kannst auch jederzeit ohne Neustart prüfen: Öffne *Einstellungen → Über* und klicke auf **Nach Updates suchen**. Bei einer installierten Version kann das Update so heruntergeladen, angewendet und neu gestartet werden - auch dann, wenn *Beim Start nach Updates suchen* (auf der Seite *App*) ausgeschaltet ist.

## GobchatEx verwenden

In deiner Tray (Symbole unten rechts) erscheint ein neues Symbol: ein Buchstabe **„G"**, dessen Farbe den Zustand von GobchatEx auf einen Blick zeigt.

- **Schwarzes „G"** - läuft, ist aber nicht mit FFXIV verbunden (es sucht nach dem Spiel).
- **Goldenes „G"** - verbunden und das Chat-Overlay ist sichtbar.
- **Schwarzes „G" mit goldener Umrandung** - verbunden, aber das Overlay ist gerade ausgeblendet (z. B. im Titelbildschirm oder automatisch, weil ein anderes Fenster im Vordergrund ist).

**Tray-Icon:** Linksklick zeigt/versteckt das Overlay, Rechtsklick öffnet ein Kontextmenü.

**Tastenkürzel** (*Einstellungen → App → Tastenkürzel*, inaktiv bis zugewiesen):
- **Ein- & ausblenden** - zeigt oder versteckt das Overlay.
- **Suche fokussieren** - holt das Overlay in den Vordergrund und setzt den Cursor direkt in das Suchfeld.

<details>
<summary>Hinweise zum Betrieb, Administratorrechte &amp; Schließen</summary>

- Das Overlay ist nicht vor FFXIV sichtbar, wenn das Spiel im **Vollbildmodus** läuft (nutze randlos/Fenster). GobchatEx wurde für FFXIV 64-Bit, DirectX 11 geschrieben.
- Bis der goldene (verbundene) Zustand erreicht ist, kann es beim ersten Start eine Weile dauern. Solange GobchatEx noch auf FFXIV wartet, wird ein Begrüßungsfenster angezeigt; dessen **X**-Button beendet GobchatEx.
- GobchatEx benötigt keine Administratorrechte und startet ohne UAC-Abfrage. Einzige Ausnahme: Wenn **FFXIV selbst als Administrator läuft**, kann GobchatEx dessen Chat nicht auslesen und fragt nach, ob es sich als Administrator neu starten soll. Als Faustregel: starte beide als Administrator oder keines von beiden.
- **Zum Schließen:** Rechtsklick auf das Tray-Icon → *close*.

</details>

## Fehlerbehebung

<details>
<summary>Häufige Probleme</summary>

**Der Reichweitenfilter scheint nicht zu funktionieren** - öffne die Konfigurationsseite *Reichweitenfilter*; kann GobchatEx keine Spieler aus FFXIV auslesen, erklärt eine rote Meldung die Ursache. Stelle sicher, dass FFXIV läuft, öffne den Konfigurationsdialog neu (GobchatEx braucht einen Moment zum Laden) oder schließe GobchatEx und lösche den Ordner `sharlayan` unter `resources`, damit er neu heruntergeladen wird.

**GobchatEx startet nicht** - prüfe `gobchatex_debug.log`. Eine Fehlermeldung, die die **WebView2**-Runtime erwähnt, bedeutet, dass die Microsoft Edge WebView2 Runtime fehlt oder veraltet ist - installiere sie über den Link unter [Installation](#installation).

**„GobchatEx is already running“** - es kann immer nur eine GobchatEx-Instanz gleichzeitig laufen. Startest du es, während es bereits offen ist, erscheint dieser Hinweis und die zweite Kopie schließt sich - die laufende findest du bei der Uhr im Infobereich (ihr „G“-Symbol).

</details>

## Unterstützen

Wenn GobchatEx dein Rollenspiel angenehmer macht, kannst du die Entwicklung auf Ko-fi unterstützen - vielen Dank! 💛

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/X6W621UWH7)

## Lizenz

Diese Software ist freie Software: Du kannst sie unter den Bedingungen der GNU Affero General Public License (**AGPL-3.0-only**), wie von der Free Software Foundation veröffentlicht, Version 3, weitergeben und/oder verändern. Siehe den [vollständigen Lizenztext](LICENSE.md) oder besuche <https://www.gnu.org/licenses/>.

> GobchatEx ist ein Fork von [Gobchat](https://github.com/MarbleBag/Gobchat) von MarbleBag, lizenziert unter AGPL-3.0.

---

<sub>© 2010-2026 SQUARE ENIX CO., LTD. All Rights Reserved. A REALM REBORN is a registered trademark or trademark of Square Enix Co., Ltd. FINAL FANTASY, SQUARE ENIX and the SQUARE ENIX logo are registered trademarks or trademarks of Square Enix Holdings Co., Ltd.</sub>
