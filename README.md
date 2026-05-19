# ⚡ PureUpdate (Unified Update Manager)

![Platform](https://img.shields.io/badge/Platform-Windows%2011-0078D4?logo=windows&style=flat-square)
![Framework](https://img.shields.io/badge/.NET-8.0%20%7C%20Native%20AOT-512BD4?logo=dotnet&style=flat-square)
![UI](https://img.shields.io/badge/UI-WPF--UI%20(Fluent)-0078D4?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

**PureUpdate** est un gestionnaire de mises à jour système et logiciel ultra-portable, conçu avec l'esthétique Fluent Design de Windows 11. Il centralise Windows Update, Winget, Scoop, Chocolatey et Snappy Driver Installer (SDI) au sein d'une seule interface fluide et intuitive.

Idéal pour les environnements de diagnostic (WinPE), le déploiement sur clé USB ou la maintenance rapide de parcs informatiques.

---

## ✨ Fonctionnalités Principales

- **Centralisation Absolue :** Scan et installation des mises à jour via `Windows Update API (WUAPI)`, `Winget`, `Chocolatey`, et `Scoop` depuis un tableau de bord unique.
- **Mode Hors-Ligne (Fallback) :** Intégration native de **Snappy Driver Installer (SDI)**. Si aucune connexion réseau n'est détectée, l'application bascule sur l'installation des pilotes en mode hors-ligne.
- **Totalement Portable :** Compilé en `Native AOT` (Ahead-of-Time). Un seul fichier `.exe` autonome, sans aucune dépendance au framework .NET requise sur la machine cible.
- **Interface Windows 11 (Fluent Design) :** Utilisation de `WPF-UI` pour une intégration visuelle parfaite avec l'OS (effets Mica, coins arrondis, mode sombre/clair automatique).
- **Exécution Asynchrone :** L'interface ne gèle jamais. Les processus CLI et COM sont exécutés silencieusement en arrière-plan avec parsing des résultats en temps réel.
- **Nettoyage Automatique :** Purge des caches de téléchargement (SoftwareDistribution, Temp) après les mises à jour pour libérer de l'espace disque.

---

## 🚀 Installation & Utilisation

Aucune installation n'est requise. PureUpdate est une application portable (Standalone).

1. Téléchargez la dernière version dans l'onglet [Releases](../../releases).
2. Lancez `PureUpdate.exe` (L'application demandera automatiquement les privilèges Administrateur via l'UAC).
3. **Pour l'utilisation hors-ligne :** Placez le dossier `SDI` contenant `sdi64.exe` (ou `SDI_auto.bat`) dans le même répertoire que l'exécutable `PureUpdate.exe`. L'application le détectera automatiquement.

---

## 🛠️ Architecture Technologique (Tech Stack)

Ce projet respecte une architecture modulaire stricte : **1 Fichier = 1 Responsabilité Logique.**

- **Langage :** C# 12
- **Framework Core :** .NET 8 (ou 9)
- **Framework UI :** WPF (Windows Presentation Foundation) + `WPF-UI` (Lepoco)
- **Interfaçage Système :** 
  - `WUApiLib` (COM) pour Windows Update.
  - `System.Diagnostics.Process` Wrappers pour Winget/Choco/Scoop CLI.

---

## 📁 Structure du Projet (Aperçu)

```text
PureUpdate/
├── Core/
│   ├── Providers/
│   │   ├── WindowsUpdateManager.cs   # Wrapper WUAPI COM
│   │   ├── WingetManager.cs          # CLI Wrapper asynchrone pour Winget
│   │   ├── ChocoManager.cs           # CLI Wrapper pour Chocolatey
│   │   └── ScoopManager.cs           # CLI Wrapper pour Scoop
│   └── Offline/
│       └── SnappyIntegrator.cs       # Détection et exécution silencieuse de SDI
├── UI/
│   ├── Views/                        # Pages Fluent Design (Dashboard, Settings, Logs)
│   └── ViewModels/                   # Logique d'interface (MVVM)
└── Utils/
    ├── Logger.cs                     # Gestion des erreurs et logs locaux
    └── PrivilegeHelper.cs            # Vérification et élévation UAC
