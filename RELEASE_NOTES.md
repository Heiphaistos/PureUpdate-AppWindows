# Release Notes — PureUpdate

---

## v1.6.0 — 2026-07-18

### Correction majeure — installations Winget
- **Les installations Winget échouaient en masse sur les machines avec beaucoup de paquets** : winget tronque les colonnes de ses tables à 120 caractères (avec « … ») quand sa sortie est redirigée, donc les IDs longs parsés étaient invalides et chaque `upgrade --id` échouait avec NO_APPLICATIONS_FOUND (0x8A150014) — masqué de surcroît en « MS Store bloqué ». Correctifs :
  - scan et liste exécutés dans une console cachée élargie à 512 colonnes (plus aucune troncature à la source) ;
  - tout ID contenant « … » est résolu via `winget list` avant upgrade/désinstallation, avec retry automatique sur 0x8A150014 ;
  - 0x8A150014 est désormais remonté comme un vrai échec avec le nom du paquet (page Erreurs).
- Validé en VM : 7-Zip 24.09→26.02 et Notepad++ 8.9→8.9.7 réellement mis à jour via la carte Winget, listes re-scannées propres.

---

## v1.5.0 — 2026-07-18

### Corrections
- **Encodage des sorties PowerShell** : les noms de périphériques accentués s'affichaient corrompus sur la page Pilotes (« Clich� instantan� ») — la sortie OEM de PowerShell était lue en UTF-8. Corrigé pour la page Pilotes, l'historique Get-HotFix et les commandes Scoop.
- **Versions affichées** : « À propos » affichait 1.3.0 et le pied de page des exports HTML 1.2.0 (valeurs codées en dur) — toutes les versions affichées proviennent désormais de l'assembly.
- **Statut de carte après masquage** : masquer la dernière mise à jour d'un provider laissait l'ancien compteur affiché dans le statut de la carte.
- **Page Pilotes** : les périphériques fantômes (déconnectés, Status=Unknown) et les classes virtuelles sont maintenant exclus du scan ; seuls Error/Degraded sont signalés comme problèmes.

### Installeur
- Nouveau **setup Inno Setup** (`PureUpdate_vX.Y.Z_win-x64_Setup.exe`) : installation dans Program Files, icônes menu Démarrer + bureau (optionnel), désinstallation propre incluant les données locales.

### Validation
- Campagne de tests complète en VM Windows 11 : scan/installation réels Windows Update et Winget, désinstallation réelle d'un paquet Chocolatey, exports HTML/CSV, masquage de mises à jour, filtre de recherche, pages Pilotes/Erreurs/Historique/Désinstaller, thèmes et paramètres.

---

## v1.4.0 — 2026-05-30

### Thèmes & Personnalisation
- **12 thèmes prédéfinis** avec cartes de prévisualisation dans Paramètres : Deep Space, Midnight, Forest, Crimson, Amber, Arctic, Obsidian, Sakura, Matrix, Solar, Neon Purple, Gold
- **Couleur d'accentuation personnalisée** : saisie hex + aperçu live (border coloré) + application instantanée sans redémarrage
- **9 polices d'interface** : ajout Roboto, Inter, JetBrains Mono, Fira Code
- Application de thème dynamique via DynamicResource WPF (ElectricCyan, AppBg, CardBg1, CardBg2)

### Dashboard — Nouvelles fonctionnalités
- **Masquer une mise à jour** : bouton ✕ par ligne pour exclure définitivement une mise à jour bloquée (ex: Microsoft Edge 0x8A15002B) — persisté dans `hidden_updates.json`, le score de santé remonte à 100 %
- **Barre de progression déterministe** : affichage `X / Y` avec label numérique pendant les installations (convention `[N/M] message` sur IProgress)
- **Champ de recherche/filtre** : filtre instantané par nom dans chaque provider card (ICollectionView)

### Nouvelle page — Pilotes
- Scan des pilotes via `Get-PnpDevice` (PowerShell/WMI, sans dépendance externe)
- Exclut les périphériques fantômes (déconnectés) et les classes virtuelles (SoftwareDevice, WPD, VolumeSnapshot)
- Seuls les statuts `Error` et `Degraded` sont signalés comme problèmes réels
- Filtre «Problèmes seulement», recherche par nom/fabricant/classe
- Raccourci Gestionnaire de périphériques

### Nouvelle page — Désinstaller
- Désinstallation d'applications via les providers disponibles

### Nouvelle page — Erreurs
- Agrégation en temps réel des erreurs d'installation (tous providers)

### Comportement système
- **Analyser au démarrage** : nouvelle option dans les Paramètres
- **Badge tray** : icône système affiche le nombre de mises à jour en attente (DrawingVisual + RenderTargetBitmap)
- **Mode headless `--scan`** : scan silencieux sans fenêtre, pour planificateur ou scripts
- **Winget** : retry automatique avec `--force` sur erreur 0x8A15002B (AGREEMENT_NOT_ACCEPTED)
- **Chocolatey** : `--accept-license` ajouté à la commande d'upgrade
- **Windows Update** : progression en 3 phases `[1/3]` `[2/3]` `[3/3]`

---

## v1.3.0 — 2026-05-23

### Nouvelles fonctionnalités
- **Onglet Erreurs** dans l'Historique : `InstallErrorStore` agrège en temps réel toutes les erreurs d'installation de tous les providers
- **Reporting d'erreurs en direct** : chaque erreur est capturée et affichée immédiatement sans attendre la fin de l'opération
- **Health Score 100/100** : 0 mise à jour en attente → score parfait
- **Détection installation manuelle** (Winget) : codes exit spécifiques + mots-clés output → label «manuelle requise»

### UI Redesign
- Sidebar avec indicateur actif gauche cyan et logo avec glow
- Dashboard : health ring card, status cards côte-à-côte, provider cards avec bande accent gauche
- Logs : onglets modern underline, colonnes alignées, onglet Erreurs dédié
- Settings : icônes Segoe MDL2 par section

### Corrections
- Onglets Chocolatey et Scoop : bouton «Charger» manquant → ajouté
- `IsInstallingAll` : progress bar désormais visible dans le header du dashboard
- Couleurs trop sombres sur certains écrans → corrigées

---

## v1.2.0 — 2026-05-20

### Nouvelles fonctionnalités
- **Sélection par checkbox** : choix granulaire des paquets à installer par provider card
- **Liste des paquets** : affichage détaillé (nom, version actuelle, version cible)
- **Thèmes personnalisables** : sélecteur de palette couleur dans les paramètres
- **Fix winget installs** : gestion correcte des codes de sortie
- **Page Historique** : persistance et affichage de l'historique des installations

---

## v1.1.0 — 2026-05-18

### Corrections (audit complet)
- Fix démarrage : deadlock `sync-over-async` sur le dispatcher WPF
- Fix scan Windows Update : wrapper WUAPI COM stabilisé
- Fix annulation : `process.Kill(entireProcessTree: true)`
- Fix logs : encodage UTF-8 forcé sur stdout/stderr
- Fix thème : détection correcte du mode système

---

## v1.0.0 — 2026-05-15

### Version initiale
- 4 providers : Windows Update (WUAPI), Winget, Chocolatey, Scoop
- Mode hors-ligne : intégration Snappy Driver Installer (SDI)
- Interface WPF-UI Fluent Design (Mica, coins arrondis, dark/light auto)
- Health Score 0–100
- MVVM avec CommunityToolkit.Mvvm
- Exécution 100 % asynchrone
- Élévation UAC automatique
- Notifications tray, point de restauration système, planificateur, export CSV
- Self-contained .NET 8 (aucune dépendance requise sur la cible)
