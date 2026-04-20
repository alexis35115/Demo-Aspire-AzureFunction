# Demo Aspire — Azure Functions avec Service Bus

Démonstration d'une application [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview) orchestrant une **Azure Function** déclenchée par **Azure Service Bus**, avec persistance des messages dans une base de données **SQL Server**.

## Table des matières

- [Architecture](#architecture)
- [Prérequis](#prérequis)
- [Structure de la solution](#structure-de-la-solution)
- [Démarrage rapide](#démarrage-rapide)
- [Tester l'envoi d'un message](#tester-lenvoi-dun-message)

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    .NET Aspire AppHost                  │
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

| Outil | Version minimale | Lien |
|---|---|---|
| .NET SDK | 9.0 | https://dotnet.microsoft.com/download |
| .NET Aspire workload | 9.x | `dotnet workload install aspire` |
| Docker Desktop | Dernière version stable | https://www.docker.com/products/docker-desktop |
| Azure Functions Core Tools | 4.x | https://learn.microsoft.com/azure/azure-functions/functions-run-local |
| Visual Studio | 2022 17.12+ ou 2026 | https://visualstudio.microsoft.com |

> **Docker est obligatoire** : l'émulateur Service Bus et le conteneur SQL Server s'exécutent tous les deux dans Docker.

---

## Structure de la solution

```
aspire-functions-service-bus-trigger/
├── AspireApp.AppHost/                  # Orchestrateur .NET Aspire
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

1. Le conteneur **Service Bus** (émulateur)
2. Le conteneur **SQL Server**
3. Le projet **DbMigrator** (création de la table)
4. La **Azure Function** (une fois la BD prête)

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
