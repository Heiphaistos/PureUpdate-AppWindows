# ⚡ PureUpdate

![Version](https://img.shields.io/badge/version-1.3.0-blue?style=flat-square)
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
- **Health Score** : score 0–100 basé sur les mises à jour en attente et l'état de redémarrage (100/100 = tout à jour)
- **Provider Cards** : état par provider avec liste des paquets à mettre à jour, sélection par checkbox
- **Scan global** : `Tout analyser` lance les 4 providers en parallèle
- **Tout installer** : installation séquentielle de tous les paquets sélectionnés
- **Statut redémarrage** : détection via clé registre `PendingFileRenameOperations`
- **Détection manuelle** : codes exit Winget spécifiques → label «installation manuelle requise» au lieu d'erreur

### Historique & Logs
- **Onglet Logs** : flux en temps réel de l'output CLI de chaque provider
- **Onglet Historique** : installations passées avec statut (succès / erreur / manuelle)
- **Onglet Erreurs** : agrégation en temps réel de toutes les erreurs d'installation (tous providers confondus)
- Export CSV de l'historique

### Paramètres
- Thème clair / sombre / automatique (Mica Windows 11)
- Notifications système (tray) : mises à jour disponibles, redémarrage requis
- Planificateur de scan automatique
- Point de restauration système avant installation

### Exécution
- **100 % asynchrone** : interface jamais bloquée, opérations CLI en arrière-plan
- **Élévation UAC** automatique au démarrage (requis pour WUAPI + schtasks)
- **Annulation** : toute opération en cours peut être annulée via `CancellationToken`
- **ANSI stripping** : output CLI nettoyé avant affichage

---

## Installation

### Option 1 : Portable (recommandée)

1. Télécharger `PureUpdate_v1.3.0_win-x64.zip` depuis les [Releases](https://github.com/heiphaistos44-crypto/PureUpdate-/releases/latest)
2. Extraire l'archive
3. Lancer `PureUpdate.exe` (UAC demandé automatiquement)

### Option 2 : Installeur

Télécharger `PureUpdate_v1.3.0_win-x64_Setup.exe` — wizard d'installation, raccourcis Bureau & Menu Démarrer, désinstalleur intégré.

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
│   │   ├── WindowsUpdateManager.cs     # WUAPI COM wrapper
│   │   ├── WingetManager.cs            # Winget CLI + détection manuelle
│   │   ├── ChocoManager.cs             # Chocolatey CLI
│   │   └── ScoopManager.cs             # Scoop CLI
│   ├── Models/
│   │   ├── UpdateItem.cs               # Paquet à mettre à jour
│   │   ├── UpdateResult.cs             # Résultat d'installation
│   │   ├── HistoryItem.cs              # Entrée historique
│   │   ├── InstallError.cs             # Erreur d'installation
│   │   └── AppSettings.cs              # Configuration persistée
│   ├── Services/
│   │   ├── AppSettingsService.cs       # Persistance JSON des paramètres
│   │   ├── ExportService.cs            # Export CSV historique
│   │   ├── InstallErrorStore.cs        # Agrégation erreurs multi-providers
│   │   ├── NotificationService.cs      # Notifications tray Windows
│   │   ├── RebootRequiredService.cs    # Détection redémarrage requis
│   │   ├── RestorePointService.cs      # Point de restauration système
│   │   ├── SchedulerService.cs         # Planification scan automatique
│   │   ├── ThemeService.cs             # Thème Fluent (Mica)
│   │   └── WindowsUpdateHistoryService.cs # Historique Windows Update
│   └── Offline/
│       └── SnappyIntegrator.cs         # Détection + lancement SDI
├── UI/
│   ├── ViewModels/
│   │   ├── DashboardViewModel.cs       # Orchestrateur principal (MVVM)
│   │   ├── HealthScoreViewModel.cs     # Calcul score santé
│   │   ├── ProviderCardViewModel.cs    # État et actions par provider
│   │   ├── LogsViewModel.cs            # Logs + historique + erreurs
│   │   └── SettingsViewModel.cs        # Paramètres
│   └── Views/
│       ├── DashboardPage.xaml(.cs)     # Page principale
│       ├── LogsPage.xaml(.cs)          # Logs / Historique / Erreurs
│       ├── SettingsPage.xaml(.cs)      # Paramètres
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
            │       └── CliProviderBase.RunAsync(exe, args)  ← ProcessStartInfo, pas de shell
            │               └── Parse output → List<UpdateItem>
            │
            └── HealthScoreViewModel.Update(pendingCount, rebootRequired)

Bouton "Tout installer"
    │
    └── DashboardViewModel.InstallAllAsync()
            │
            ├── RestorePointService.CreateAsync()  (optionnel)
            └── ProviderCardViewModel.InstallAsync() × N (séquentiel)
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
| Package managers | Winget, Chocolatey, Scoop | CLI |
| Installeur | Inno Setup | `installer.iss` |

---

## Build

```bat
:: Publier l'exécutable portable
build.bat

:: Résultat : publish\PureUpdate.exe (self-contained, ~60 MB)
```

L'installeur NSIS est généré séparément via `installer.iss` (Inno Setup).

---

## Sécurité

- **Pas d'injection shell** : `ProcessStartInfo(exe, args)` direct, jamais `cmd /c "..."`
- **ANSI stripping** : output CLI nettoyé avant parsing (pas d'ANSI escape injection)
- **UAC** : élévation demandée une seule fois au démarrage, pas de bypass
- **Annulation propre** : `process.Kill(entireProcessTree: true)` sur annulation

---

## Licence

MIT

---

*PureUpdate — Maintenance Windows centralisée, portable et sans friction*
