# Demo Aspire — Azure Functions avec Service Bus

Démonstration d'une application [Aspire](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview) orchestrant une **Azure Function** déclenchée par **Azure Service Bus**, avec persistance des messages dans une base de données **SQL Server**.

## Table des matières

- [Architecture](#architecture)
- [Prérequis](#prérequis)
- [Outils et émulateurs de développement](#outils-et-émulateurs-de-développement)
- [Structure de la solution](#structure-de-la-solution)
- [Démarrage rapide](#démarrage-rapide)
- [Tester l'envoi d'un message](#tester-lenvoi-dun-message)

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Aspire AppHost                  │
│                                                         │
│  ┌──────────────────┐      ┌──────────────────────────┐ │
│  │  Service Bus     │      │  SQL Server              │ │
│  │  (émulateur)     │      │  Base : Communication    │ │
│  │  File : myqueue  │      │                          │ │
│  └────────┬─────────┘      └────────────┬─────────────┘ │
│           │                             │               │
│           │          ┌──────────────────┴─────────┐     │
│           │          │  DbMigrator                │     │
│           │          │  (crée la table au boot)   │     │
│           │          └────────────────────────────┘     │
│           │                             │               │
│           └──────────────┬──────────────┘               │
│                          │                              │
│                ┌─────────▼──────────┐                   │
│                │  Azure Function    │                   │
│                │  (ServiceBusTrigger│                   │
│                │   → INSERT en BD)  │                   │
│                └────────────────────┘                   │
└─────────────────────────────────────────────────────────┘
```

Le flux est le suivant :

1. Un message JSON est déposé dans la file `myqueue`.
2. La **Azure Function** est déclenchée automatiquement.
3. Le message est désérialisé en un objet `Communication`.
4. L'objet est inséré dans la table `[dbo].[Communication]` de SQL Server.
5. En cas d'erreur de désérialisation, le message est envoyé en **dead-letter**. En cas d'autre erreur, il est abandonné.

---

## Prérequis

### Outils à installer manuellement

| Outil | Version minimale | Lien |
|---|---|---|
| .NET SDK | 9.0 | https://dotnet.microsoft.com/download |
| Aspire workload | 9.x | `dotnet workload install aspire` |
| Docker Desktop | Dernière version stable | https://www.docker.com/products/docker-desktop |
| Azure Functions Core Tools | 4.x | https://learn.microsoft.com/azure/azure-functions/functions-run-local |
| Visual Studio | 2022 17.12+ ou 2026 | https://visualstudio.microsoft.com |

> **Docker est obligatoire** : tous les émulateurs et conteneurs de cette solution sont orchestrés par Aspire et démarrés automatiquement via Docker.

### Vérifier les prérequis

```powershell
# Vérifier .NET 9
dotnet --version

# Vérifier le workload Aspire
dotnet workload list

# Vérifier Azure Functions Core Tools
func --version

# Vérifier Docker
docker --version
docker info
```

---

## Outils et émulateurs de développement

Cette solution utilise plusieurs émulateurs locaux pour reproduire l'environnement Azure **sans dépendre de ressources cloud**. Ils sont tous gérés automatiquement par Aspire au démarrage — aucune configuration manuelle n'est requise.

### Azure Service Bus Emulator

| Propriété | Valeur |
|---|---|
| Image Docker | `mcr.microsoft.com/azure-messaging/servicebus-emulator` |
| Géré par | `Aspire.Hosting.Azure.ServiceBus` avec `.RunAsEmulator()` |
| Persistance | Activée (`ContainerLifetime.Persistent`) |
| File configurée | `myqueue` |

L'émulateur Service Bus reproduit fidèlement le comportement d'Azure Service Bus en local. La persistance est activée : le conteneur Docker n'est pas recréé à chaque redémarrage d'Aspire, ce qui conserve les messages non consommés entre les sessions.

> L'émulateur Service Bus requiert **Azurite** pour son stockage interne. Aspire démarre Azurite automatiquement en tant que dépendance.

### Azurite (émulateur Azure Storage)

| Propriété | Valeur |
|---|---|
| Image Docker | `mcr.microsoft.com/azure-storage/azurite` |
| Géré par | Aspire, en tant que dépendance de l'émulateur Service Bus et des Azure Functions |
| Services émulés | Blob, Queue, Table |

Azurite émule les services de stockage Azure. Il est utilisé à deux endroits :
- Par l'**émulateur Service Bus** pour son stockage interne.
- Par le **runtime Azure Functions v4**, qui requiert un compte de stockage pour la coordination de l'hôte (`AzureWebJobsStorage`).

### SQL Server (conteneur)

| Propriété | Valeur |
|---|---|
| Image Docker | `mcr.microsoft.com/mssql/server` |
| Géré par | `Aspire.Hosting.SqlServer` |
| Base de données | `Communication` |
| Authentification | Compte SA avec mot de passe via secret Aspire (`sql-sa-password`) |

Le conteneur SQL Server est provisionné par Aspire. La base de données `Communication` et son schéma sont créés automatiquement au démarrage par le projet **DbMigrator**.

### Résumé des conteneurs Docker

Au démarrage, Aspire orchestre automatiquement les conteneurs suivants :

| Conteneur | Image | Rôle |
|---|---|---|
| Service Bus Emulator | `mcr.microsoft.com/azure-messaging/servicebus-emulator` | Broker de messages |
| Azurite | `mcr.microsoft.com/azure-storage/azurite` | Stockage local pour Service Bus et Functions |
| SQL Server | `mcr.microsoft.com/mssql/server` | Base de données relationnelle |

---

## Structure de la solution

```
aspire-functions-service-bus-trigger/
├── AspireApp.AppHost/                  # Orchestrateur Aspire
│   ├── Program.cs                      # Déclaration de toutes les ressources
│   └── Extensions/
│       └── ServiceBusExtensions.cs     # Commande de test "Send Service Bus message"
│
├── AspireApp.ServiceDefaults/          # Configuration partagée (OpenTelemetry, health checks…)
│   └── Extensions.cs
│
├── AspireApp.DbMigrator/               # Projet console — migration de la base de données
│   └── Program.cs                      # Crée la table [dbo].[Communication] au démarrage
│
└── AspireApp.FunctionApp/              # Azure Function
    ├── Program.cs                      # Configuration du host
    └── ServiceBusFunction.cs           # Trigger Service Bus + insertion SQL
```

### Ressources Aspire orchestrées

| Ressource | Type | Description |
|---|---|---|
| `myservicebus` | Azure Service Bus (émulateur) | Broker de messages, persistance activée |
| `myqueue` | File Service Bus | File d'attente consommée par la Function |
| `sql` | SQL Server (conteneur) | Base de données relationnelle |
| `Communication` | Base de données SQL | Stockage des messages traités |
| `dbmigrator` | Projet console | Crée le schéma avant le démarrage de la Function |
| `functionapp` | Azure Functions | Traitement des messages entrants |

---

## Démarrage rapide

### 1. Configurer le mot de passe SQL Server

Le mot de passe SA de SQL Server est injecté via un paramètre secret Aspire. Créez le secret en local :

```powershell
dotnet user-secrets --project AspireApp.AppHost set "Parameters:sql-sa-password" "VotreMotDePasse123!"
```

> Le mot de passe doit respecter la politique de complexité SQL Server (majuscule, minuscule, chiffre, caractère spécial, 8 caractères minimum).

### 2. Lancer la solution

```powershell
dotnet run --project AspireApp.AppHost
```

Aspire démarre automatiquement dans l'ordre :

1. Le conteneur **Azurite** (stockage local)
2. Le conteneur **Service Bus** (émulateur)
3. Le conteneur **SQL Server**
4. Le projet **DbMigrator** (création de la table)
5. La **Azure Function** (une fois la BD prête)

Le tableau de bord Aspire s'ouvre dans le navigateur à l'adresse indiquée dans la console (généralement `https://localhost:15888`).

---

## Tester l'envoi d'un message

Une commande de test est intégrée directement dans le tableau de bord Aspire :

1. Ouvrir le tableau de bord Aspire.
2. Naviguer vers la ressource **`myqueue`**.
3. Cliquer sur **« Send Service Bus message »**.

Un message JSON de la forme suivante est envoyé :

```json
{
  "Id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "Type": "Email",
  "Destination": "user@example.com",
  "Subject": "Welcome",
  "Body": "Hello from Aspire!",
  "RequestedAtUtc": "2025-01-01T00:00:00+00:00"
}
```

La Function est déclenchée et insère l'enregistrement dans la table `[dbo].[Communication]`. Le résultat est visible dans les journaux de la ressource `functionapp` du tableau de bord.