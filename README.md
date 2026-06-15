# ⚡ PureUpdate

## Démonstration

https://github.com/heiphaistos44-crypto/PureUpdate-/releases/download/v1.4.0/pureupdate.mp4


![Version](https://img.shields.io/badge/version-1.4.0-blue?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4?logo=windows&style=flat-square)
![Framework](https://img.shields.io/badge/.NET-8.0%20Self--Contained-512BD4?logo=dotnet&style=flat-square)
![UI](https://img.shields.io/badge/UI-WPF--UI%20Fluent-0078D4?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

**Gestionnaire de mises à jour unifié pour Windows** — Windows Update, Winget, Chocolatey, Scoop et SDI hors-ligne depuis une seule interface Fluent Design.

Un seul `.exe` portable, sans dépendance .NET requise sur la machine cible.

---

## Fonctionnalités

### 4 Providers intégrés

| Provider | Protocole | Packages couverts |
|----------|-----------|-------------------|
| **Windows Update** | WUAPI COM | Mises à jour système, pilotes Microsoft, KB |
| **Winget** | CLI | Applications Windows (Microsoft Store + sources tierces) |
| **Chocolatey** | CLI | Packages Choco |
| **Scoop** | CLI | Applications Scoop |

### Mode Hors-Ligne (SDI)
Si aucune connexion réseau n'est détectée, bascule automatiquement sur **Snappy Driver Installer** — détecte et installe les pilotes manquants depuis une base locale.  
Placer le dossier `SDI/` contenant `sdi64.exe` dans le même répertoire que `PureUpdate.exe`.

### Dashboard
- **Health Score** : score 0–100 basé sur les mises à jour en attente (100/100 = tout à jour)
- **Provider Cards** : état par provider avec liste des paquets, sélection par checkbox
- **Barre de progression déterministe** : affichage `X / Y` pendant les installations
- **Champ de recherche** : filtre instantané par nom dans chaque provider card
- **Masquer une mise à jour** : bouton ✕ par ligne pour masquer définitivement une mise à jour bloquée — le score remonte à 100 %
- **Scan global** : `Tout analyser` lance les 4 providers en parallèle
- **Détection manuelle** : codes exit Winget spécifiques → label «installation manuelle requise»
- **Statut redémarrage** : détection via clé registre `PendingFileRenameOperations`

### Page Pilotes
- Scan via `Get-PnpDevice` PowerShell (WMI natif, sans dépendance externe)
- Affiche uniquement les périphériques **présents** (exclut les fantômes/déconnectés)
- Filtre «Problèmes seulement» — ne signale que les statuts `Error` et `Degraded`
- Raccourci vers le Gestionnaire de périphériques Windows

### Page Désinstaller
- Désinstallation d'applications via les providers disponibles

### Page Erreurs
- Agrégation en temps réel de toutes les erreurs d'installation (tous providers confondus)

### Historique & Logs
- **Onglet Logs** : flux CLI en temps réel
- **Onglet Historique** : installations passées avec statut (succès / erreur / manuelle)
- **Onglet Erreurs** : erreurs session + parsing du fichier log
- Export CSV de l'historique

### Thèmes & Personnalisation
- **12 thèmes prédéfinis** avec cartes de prévisualisation : Deep Space, Midnight, Forest, Crimson, Amber, Arctic, Obsidian, Sakura, Matrix, Solar, Neon Purple, Gold
- **Couleur d'accentuation personnalisée** : saisie hex + aperçu live + application instantanée
- **9 polices d'interface** : Segoe UI, Segoe UI Variable, Roboto, Inter, Arial, Calibri, Consolas, JetBrains Mono, Fira Code
- Changement de thème sans redémarrage (DynamicResource WPF)

### Paramètres
- **Analyser au démarrage** : scan automatique à chaque ouverture
- **Fermer dans la barre système** (close-to-tray)
- **Point de restauration** avant chaque installation
- **Planificateur** : scan automatique quotidien ou hebdomadaire (schtasks.exe)
- **Badge tray** : nombre de mises à jour en attente affiché sur l'icône système

### Exécution
- **Mode headless `--scan`** : lance un scan silencieux en ligne de commande (pas de fenêtre)
- **100 % asynchrone** : interface jamais bloquée
- **Élévation UAC** automatique au démarrage (requis pour WUAPI + schtasks)
- **Annulation** : toute opération annulable via `CancellationToken`
- **Retry automatique** : Winget relance avec `--force` sur erreur 0x8A15002B (AGREEMENT_NOT_ACCEPTED)

---

## Installation

### Option 1 : Portable (recommandée)

1. Télécharger `PureUpdate_v1.4.0_win-x64.zip` depuis les [Releases](https://github.com/heiphaistos44-crypto/PureUpdate-/releases/latest)
2. Extraire l'archive
3. Lancer `PureUpdate.exe` (UAC demandé automatiquement)

### Prérequis
- Windows 10 / 11 (x64)
- Droits administrateur
- Connexion internet (optionnelle — mode SDI hors-ligne disponible)

---

## Architecture

```
PureUpdate/
├── Core/
│   ├── Providers/
│   │   ├── CliProviderBase.cs          # Base CLI (ProcessStartInfo, ANSI strip, parse)
│   │   ├── IUpdateProvider.cs          # Interface scan + install
│   │   ├── ISelfManagedProvider.cs     # Interface auto-install provider
│   │   ├── IUninstallProvider.cs       # Interface désinstallation
│   │   ├── WindowsUpdateManager.cs     # WUAPI COM wrapper
│   │   ├── WingetManager.cs            # Winget CLI + retry AGREEMENT_NOT_ACCEPTED
│   │   ├── ChocoManager.cs             # Chocolatey CLI
│   │   └── ScoopManager.cs             # Scoop CLI
│   ├── Models/
│   │   ├── UpdateItem.cs               # Paquet à mettre à jour
│   │   ├── UpdateResult.cs             # Résultat d'installation
│   │   ├── HistoryItem.cs              # Entrée historique
│   │   ├── InstallError.cs             # Erreur d'installation
│   │   ├── UninstallableItem.cs        # Application désinstallable
│   │   ├── UninstallResult.cs          # Résultat désinstallation
│   │   └── AppSettings.cs              # Configuration persistée
│   ├── Services/
│   │   ├── AppSettingsService.cs       # Persistance JSON des paramètres
│   │   ├── ExportService.cs            # Export CSV historique
│   │   ├── HiddenUpdatesStore.cs       # Persistance des updates masquées
│   │   ├── InstallErrorStore.cs        # Agrégation erreurs multi-providers
│   │   ├── NotificationService.cs      # Notifications tray + badge count
│   │   ├── RebootRequiredService.cs    # Détection redémarrage requis
│   │   ├── RestorePointService.cs      # Point de restauration système
│   │   ├── SchedulerService.cs         # Planification scan automatique
│   │   ├── ThemeService.cs             # 12 thèmes + DynamicResource injection
│   │   └── WindowsUpdateHistoryService.cs
│   └── Offline/
│       └── SnappyIntegrator.cs         # Détection + lancement SDI
├── UI/
│   ├── ViewModels/
│   │   ├── DashboardViewModel.cs       # Orchestrateur principal (MVVM)
│   │   ├── HealthScoreViewModel.cs     # Calcul score santé
│   │   ├── ProviderCardViewModel.cs    # État, filtre, progress, hide par provider
│   │   ├── LogsViewModel.cs            # Logs + historique + erreurs
│   │   ├── SettingsViewModel.cs        # Thèmes + polices + planificateur
│   │   ├── DriversViewModel.cs         # Scan pilotes WMI + filtre
│   │   ├── ErrorsViewModel.cs          # Erreurs d'installation
│   │   ├── UninstallViewModel.cs       # Désinstallation
│   │   └── ProviderUninstallTabViewModel.cs
│   └── Views/
│       ├── DashboardPage.xaml(.cs)     # Page principale
│       ├── LogsPage.xaml(.cs)          # Logs / Historique / Erreurs
│       ├── SettingsPage.xaml(.cs)      # Paramètres + thèmes
│       ├── DriversPage.xaml(.cs)       # Page pilotes
│       ├── ErrorsPage.xaml(.cs)        # Page erreurs
│       ├── UninstallPage.xaml(.cs)     # Page désinstallation
│       └── MainWindow.xaml(.cs)        # Fenêtre principale (sidebar)
└── Utils/
    ├── Logger.cs                       # Logs structurés (fichier + console)
    └── PrivilegeHelper.cs              # UAC + détection réseau
```

### Flux d'une mise à jour

```
Bouton "Tout analyser"
    │
    └── DashboardViewModel.ScanAllAsync()
            │
            ├── ProviderCardViewModel.ScanAsync() × 4 (en parallèle)
            │       └── CliProviderBase.RunAsync(exe, args)
            │               └── Parse output → List<UpdateItem>
            │
            └── HealthScoreViewModel.Update(pendingCount, rebootRequired)

Bouton "Tout installer"
    │
    └── DashboardViewModel.InstallAllAsync()
            │
            ├── RestorePointService.CreateAsync()  (optionnel)
            └── ProviderCardViewModel.InstallAsync() × N (séquentiel)
                    │
                    ├── IProgress<string> "[N/M] message" → ProgressBar déterministe
                    └── UpdateResult → InstallErrorStore + HistoryItem
```

---

## Technologies

| Composant | Technologie | Version |
|-----------|-------------|---------|
| Langage | C# 12 | — |
| Runtime | .NET 8 (self-contained, win-x64) | 8.0 |
| UI | WPF + WPF-UI (Fluent/Mica) | 3.0.5 |
| MVVM | CommunityToolkit.Mvvm | 8.3.2 |
| Tray | Hardcodet.Wpf.TaskbarNotification | 1.0.5 |
| Windows Update | WUApiLib (COM) | natif |
| Drivers | Get-PnpDevice (PowerShell / WMI) | natif |
| Package managers | Winget, Chocolatey, Scoop | CLI |

---

## Build

```bat
:: Publier l'exécutable portable
build.bat

:: Résultat : publish\PureUpdate.exe (self-contained, ~64 MB)
```

---

## Sécurité

- **Pas d'injection shell** : `ProcessStartInfo(exe, args)` direct, jamais `cmd /c "..."`
- **ANSI stripping** : output CLI nettoyé avant parsing
- **UAC** : élévation demandée une seule fois au démarrage, pas de bypass
- **Annulation propre** : `process.Kill(entireProcessTree: true)` sur annulation

---

## Licence

MIT

---

*PureUpdate — Maintenance Windows centralisée, portable et sans friction*
