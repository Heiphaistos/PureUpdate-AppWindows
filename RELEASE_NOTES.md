# Release Notes — PureUpdate

---

## v1.3.0 — 2026-05-23

### Nouvelles fonctionnalités
- **Onglet Erreurs** dans l'Historique : `InstallErrorStore` agrège en temps réel toutes les erreurs d'installation de tous les providers (Windows Update, Winget, Chocolatey, Scoop)
- **Reporting d'erreurs en direct** : chaque erreur d'installation est capturée et affichée immédiatement dans l'onglet Erreurs sans attendre la fin de l'opération
- **Health Score 100/100** : 0 mise à jour en attente → score parfait, plus de pénalité reboot injustifiée dans le calcul
- **Détection installation manuelle** (Winget) : codes exit spécifiques (`UNSUPPORTED_INSTALLER_TYPE`, `SYSTEM_NOT_SUPPORTED`, `INSTALL_BLOCKED_BY_POLICY`) + mots-clés output → label «manuelle requise» au lieu d'erreur générique

### UI Redesign
- Sidebar avec indicateur actif gauche cyan et logo avec glow
- Dashboard : health ring card, status cards côte-à-côte, provider cards avec bande accent gauche
- Logs : onglets modern underline, colonnes alignées, onglet Erreurs dédié
- Settings : icônes Segoe MDL2 par section

### Corrections
- Onglets Chocolatey et Scoop : bouton «Charger» manquant → ajouté
- `IsInstallingAll` : progress bar désormais visible dans le header du dashboard
- Couleurs trop sombres (écran noir sur certains thèmes) → corrigées

---

## v1.2.0 — 2026-05-20

### Nouvelles fonctionnalités
- **Sélection par checkbox** : choix granulaire des paquets à installer par provider card
- **Liste des paquets** : affichage détaillé de chaque mise à jour disponible (nom, version actuelle, version cible)
- **Thèmes personnalisables** : sélecteur de palette couleur dans les paramètres
- **Fix winget installs** : gestion correcte des cas d'erreurs et des codes de sortie
- **Page Historique** : persistance et affichage de l'historique des installations

---

## v1.1.0 — 2026-05-18

### Corrections (audit complet)
- Fix démarrage : deadlock `sync-over-async` sur le dispatcher WPF → conversion async/await propre
- Fix scan Windows Update : wrapper WUAPI COM stabilisé
- Fix annulation : `process.Kill(entireProcessTree: true)` sur `OperationCanceledException`
- Fix logs : encodage UTF-8 forcé sur stdout/stderr des processus CLI
- Fix thème : détection correcte du mode système (clair/sombre)

---

## v1.0.0 — 2026-05-15

### Version initiale
- 4 providers : Windows Update (WUAPI), Winget, Chocolatey, Scoop
- Mode hors-ligne : intégration Snappy Driver Installer (SDI)
- Interface WPF-UI Fluent Design (Mica, coins arrondis, dark/light auto)
- Health Score 0–100
- MVVM avec CommunityToolkit.Mvvm
- Exécution 100 % asynchrone (interface non bloquante)
- Élévation UAC automatique
- Notifications tray (mises à jour disponibles, redémarrage requis)
- Point de restauration système avant installation
- Planificateur de scan automatique
- Export CSV de l'historique
- Self-contained .NET 8 (aucune dépendance requise sur la cible)
