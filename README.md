# ScavKRInstaller Fork

## Francais

Ce projet est un fork de [danxnader/ScavKRInstaller](https://github.com/danxnader/ScavKRInstaller).

### Ce qui change par rapport a la version officielle

- GUI supprime: installation 100% automatique au lancement.
- Dossier d'installation force: `C:\Users\<user>\Downloads\scavMULTI`.
- Telechargements temporaires: `C:\Users\<user>\Downloads\ScavKRInstaller` puis suppression des fichiers temporaires en fin d'installation.
- Installation automatique du jeu demo, de BepInEx, du mod multijoueur et de ChangeSkin.
- Installation automatique de mods locaux supplementaires (si presents dans `Downloads`):
  - `ItemSpawnerIMGUI-*/CU_ItemSpawner_Mod.dll`
  - `SkipLore-*/CU_Skip_Lore_Mod.dll`
  - `QoL Unknown-*/QoL Unknown.dll`
  - destination: `BepInEx\plugins`
  - fallback sans fichiers locaux: ces 3 mods sont embarques dans l'EXE et installes automatiquement.
- Patch automatique de `BepInEx\plugins\KrokoshaCasualtiesMP.dll` avec:
  - IP/port par defaut: `26.35.34.177:7790`
  - mot de passe par defaut: `123`
  - nom joueur guest par defaut: `grosFemboyFurry`
  - texte menu personnalise (FR + EN)
  - textes console/chat ameliorees pour mieux utiliser les commandes (`krok help`, `krok rules`, etc.)
- Generation d'un aide-memoire console: `CONSOLE_QUICK_COMMANDS.txt`.
- Lancement automatique apres installation avec creation de:
  - `Launch_AutoConnect.ps1`
  - `Launch_AutoConnect.bat`
  - raccourci Bureau `scavMULTI.lnk` avec l'icone du jeu
- Au lancement du jeu, demande Host/Guest a chaque fois:
  - Host: nom force a `furry`, detection IP locale (priorite aux IP `26.x.x.x`), copie auto dans le presse-papiers et popup d'information.
  - Guest: demande l'IP du serveur dans une popup, ajoute le port `7790` si non fourni.
- Integration OpenVPN Community:
  - tentative de lancement OpenVPN en meme temps que le jeu
  - fermeture OpenVPN a la fermeture du jeu
  - generation de `VPN_GUEST_INFO.txt`, `vpn\credentials.txt`, `vpn\guest.ovpn.template`
- Robustesse amelioree:
  - fermeture des process jeu en cours avant mise a jour des fichiers
  - copie de fichiers avec retries en cas de DLL verrouillee
  - log de lancement `Launch_AutoConnect.log` + popup explicite si erreur de lancement

### Notes

- L'installation OpenVPN peut necessiter les droits administrateur Windows.
- Si un host/guest ne demarre pas, verifier `Launch_AutoConnect.log` dans le dossier du jeu.

## English

This project is a fork of [danxnader/ScavKRInstaller](https://github.com/danxnader/ScavKRInstaller).

### What changed compared to the official version

- GUI removed: fully automatic installer on launch.
- Forced install directory: `C:\Users\<user>\Downloads\scavMULTI`.
- Temporary downloads path: `C:\Users\<user>\Downloads\ScavKRInstaller`, then temp files are deleted at the end.
- Automatic installation of game demo, BepInEx, multiplayer mod, and ChangeSkin.
- Automatic installation of additional local mods (if present in `Downloads`):
  - `ItemSpawnerIMGUI-*/CU_ItemSpawner_Mod.dll`
  - `SkipLore-*/CU_Skip_Lore_Mod.dll`
  - `QoL Unknown-*/QoL Unknown.dll`
  - destination: `BepInEx\plugins`
  - no-local-files fallback: these 3 mods are embedded in the EXE and installed automatically.
- Automatic patching of `BepInEx\plugins\KrokoshaCasualtiesMP.dll` with:
  - default IP/port: `26.35.34.177:7790`
  - default password: `123`
  - default guest player name: `grosFemboyFurry`
  - custom main menu text (FR + EN)
  - improved console/chat command guidance (`krok help`, `krok rules`, etc.)
- Generates a quick command reference file: `CONSOLE_QUICK_COMMANDS.txt`.
- Automatic post-install launch and creation of:
  - `Launch_AutoConnect.ps1`
  - `Launch_AutoConnect.bat`
  - desktop shortcut `scavMULTI.lnk` using the game icon
- Host/Guest prompt on every game launch:
  - Host: forced name `furry`, local IP detection (prefers `26.x.x.x`), automatic clipboard copy, and info popup.
  - Guest: popup asks for server IP, adds port `7790` if not provided.
- OpenVPN Community integration:
  - tries to start OpenVPN together with the game
  - stops OpenVPN when the game exits
  - generates `VPN_GUEST_INFO.txt`, `vpn\credentials.txt`, `vpn\guest.ovpn.template`
- Improved reliability:
  - closes running game processes before updating files
  - file copy retries for locked DLL scenarios
  - launcher log file `Launch_AutoConnect.log` + explicit startup error popup

### Notes

- OpenVPN installation may require Windows admin privileges.
- If host/guest startup fails, check `Launch_AutoConnect.log` in the game folder.
